using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using PasswordManager.Services.Request;
using PasswordManager.Services.Routing;
using PasswordManager.Utils;
using PasswordManager.UI.Controls;

namespace PasswordManager.UI
{
    /// <summary>
    /// 文档权限窗口（严格还原原型 app-prototype.html SCREEN 7「文档权限」）：
    /// 顶部命令条（搜索框 + 搜索 + ◀ n/N ▶ 定位 + 「有未保存的改动 / 权限已保存」旗标 + 重置 / 保存）+
    /// 左「选择部门或人员」树（twisty / 复选框 on·mixed / 部门·人员图标、勾选部门＝整部门授权、
    /// 被上级覆盖整行置灰、搜索命中词高亮与逐条定位）+
    /// 右「已选择的部门 / 已选择的员工」两清单（只保留最上层授权、路径展示、移除按钮、空状态）+
    /// 底部富文本提示行。
    /// 业务逻辑保留：GET /doc/auth/tree 加载 + POST /doc/auth/update 保存。
    /// </summary>
    public class AuthTreeForm : ThemedWindow
    {
        public static bool IsOpen { get; private set; } = false;

        private const string LockedTip = "已包含在上级部门的授权范围内，如需单独调整请先取消上级部门授权";

        private readonly string _docId;
        private readonly HttpRequestService _httpRequestService = new();

        // ===== 模型 =====
        private readonly List<PermNodeModel> _roots = new();
        private readonly Dictionary<string, PermNodeModel> _byId = new();
        private readonly List<PermNodeModel> _order = new();

        // ===== 授权状态（原型 permDeptOn / permUserOn / permSaved / permExpanded）=====
        private readonly HashSet<string> _deptOn = new();
        private readonly HashSet<string> _userOn = new();
        private HashSet<string> _savedDept = new();
        private HashSet<string> _savedUser = new();
        private readonly HashSet<string> _expanded = new();

        // ===== 搜索（原型 permQuery / permMatches / permCursor）=====
        private string _query = "";
        private List<string> _matches = new();
        private int _cursor = -1;

        private bool _loaded;
        private bool _saving;
        private List<SelItem> _deptItems = new();
        private List<SelItem> _userItems = new();

        // ===== 控件 =====
        private SearchBox _searchBox = null!;
        private SmallButton _searchBtn = null!;
        private NavArrow _prevBtn = null!;
        private NavArrow _nextBtn = null!;
        private Label _countLabel = null!;
        private BarFlag _dirtyFlag = null!;
        private BarFlag _okFlag = null!;
        private SmallButton _resetBtn = null!;
        private SmallButton _saveBtn = null!;
        private PermPane _treePane = null!;
        private PermPane _deptPane = null!;
        private PermPane _userPane = null!;
        private PermTreeView _tree = null!;
        private PermSelList _deptList = null!;
        private PermSelList _userList = null!;
        private readonly System.Windows.Forms.Timer _okFlagTimer = new() { Interval = 2400 };

        public AuthTreeForm(string docId)
        {
            _docId = docId;
            InitializeComponent();
            IsOpen = true;
            LoadAuthTreeAsync();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            IsOpen = false;
        }

        private void InitializeComponent()
        {
            Text = "文档权限";
            ShowMinimizeButton = false;
            ShowMaximizeButton = false;
            Resizable = false;
            TopMost = true;   // 悬浮于 WPS 之上（保留原实现行为）

            _searchBox = new SearchBox("搜索部门或人员")
            {
                Inner = { Enabled = false }
            };
            _searchBox.Inner.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; RunSearch(); } };
            _searchBox.TextChanged += (_, _) =>
            {
                // 清空输入即恢复完整组织树（原型 input 事件）
                if (string.IsNullOrEmpty(_searchBox.Inner.Text) && _query.Length > 0)
                {
                    _query = "";
                    _matches = new List<string>();
                    _cursor = -1;
                    UpdateNav();
                    _tree.SetCurrentKey(null);
                    _tree.Invalidate();
                }
            };

            _searchBtn = new SmallButton("搜索", SmallButtonStyle.Primary) { Enabled = false };
            _searchBtn.Click += (_, _) => RunSearch();

            _prevBtn = new NavArrow(left: true) { Enabled = false };
            _prevBtn.Click += (_, _) => MoveCursor(-1);
            _nextBtn = new NavArrow(left: false) { Enabled = false };
            _nextBtn.Click += (_, _) => MoveCursor(1);

            _countLabel = new Label
            {
                Text = "—",
                Font = Theme.MonoSmall,
                ForeColor = Theme.TextTertiary,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                AutoSize = false
            };

            _dirtyFlag = new BarFlag { Text = "有未保存的改动", Visible = false };
            _okFlag = new BarFlag { Text = "权限已保存", Ok = true, Visible = false };
            _okFlagTimer.Tick += (_, _) => { _okFlagTimer.Stop(); _okFlag.Visible = false; };

            _resetBtn = new SmallButton("重置", SmallButtonStyle.Ghost) { Enabled = false };
            _resetBtn.Click += (_, _) => ResetToSaved();

            _saveBtn = new SmallButton("保存", SmallButtonStyle.Primary) { Enabled = false };
            _saveBtn.Click += (_, _) => SaveAsync();

            _treePane = new PermPane { HeadTitle = "选择部门或人员", HeadRule = "勾选部门 ＝ 整部门授权" };
            _deptPane = new PermPane { HeadTitle = "已选择的部门", HeadRule = "只保留最上层授权" };
            _userPane = new PermPane { HeadTitle = "已选择的员工", HeadRule = "不含部门已覆盖人员" };

            _tree = new PermTreeView
            {
                GetRows = BuildRows,
                GetState = StateOf,
                GetLocked = Covered,
                GetExpanded = n => _expanded.Contains(n.Key),
                GetHighlight = MatchRange,
                EmptyTitle = "未找到匹配的部门或人员",
                EmptySub = "试试更换关键词，或清空搜索框查看全部组织。"
            };
            _tree.ToggleRequested = Toggle;
            _tree.ExpandToggled = ToggleExpand;

            _deptList = new PermSelList
            {
                DeptKind = true,
                GetItems = () => _deptItems,
                EmptyTitle = "尚未选择部门",
                EmptySub = "勾选左侧部门，即授权该部门及其全部下级。"
            };
            _deptList.RemoveRequested = RemoveNode;

            _userList = new PermSelList
            {
                DeptKind = false,
                GetItems = () => _userItems,
                EmptyTitle = "尚未选择员工",
                EmptySub = "勾选左侧人员可单独授权；已含在部门授权内的人员不在此重复列出。"
            };
            _userList.RemoveRequested = RemoveNode;

            _treePane.Controls.Add(_tree);
            _deptPane.Controls.Add(_deptList);
            _userPane.Controls.Add(_userList);

            Controls.AddRange(new Control[]
            {
                _searchBox, _searchBtn, _prevBtn, _countLabel, _nextBtn,
                _dirtyFlag, _okFlag, _resetBtn, _saveBtn,
                _treePane, _deptPane, _userPane
            });

            ApplyLayout();
            SyncDirty();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // 命令条底部分隔线（原型 .perm-bar{border-bottom:1px solid var(--divider)}）
            int y = TitleBarHeight + Theme.S(52);
            using var pen = new Pen(Theme.Divider, 1f);
            e.Graphics.DrawLine(pen, 0, y, Width, y);
        }

        private void ApplyLayout()
        {
            int winW = Theme.S(800);
            int barY = TitleBarHeight;
            int cy = barY + Theme.S(10);
            int ch = Theme.S(32);
            int btnW = Theme.S(64);
            int gap = Theme.S(8);

            _searchBox.SetBounds(Theme.S(12), cy, Theme.S(240), ch);
            _searchBtn.SetBounds(Theme.S(12) + Theme.S(240) + gap, cy, btnW, ch);
            _prevBtn.SetBounds(Theme.S(12) + Theme.S(240) + gap + btnW + gap, cy, ch, ch);
            _countLabel.SetBounds(_prevBtn.Right + Theme.S(4), cy, Theme.S(38), ch);
            _nextBtn.SetBounds(_countLabel.Right + Theme.S(4), cy, ch, ch);

            _saveBtn.SetBounds(winW - Theme.S(12) - btnW, cy, btnW, ch);
            _resetBtn.SetBounds(_saveBtn.Left - gap - btnW, cy, btnW, ch);
            int flagRight = _resetBtn.Left - gap;
            _dirtyFlag.SetBounds(flagRight - Theme.S(130), cy, Theme.S(130), ch);
            _okFlag.SetBounds(flagRight - Theme.S(130), cy, Theme.S(130), ch);

            int top = barY + Theme.S(53) + Theme.S(12);
            int paneW = Theme.S(383);
            _treePane.SetBounds(Theme.S(12), top, paneW, Theme.S(440));
            int rx = Theme.S(12) + paneW + Theme.S(10);
            _deptPane.SetBounds(rx, top, paneW, Theme.S(181));
            _userPane.SetBounds(rx, top + Theme.S(181) + Theme.S(10), paneW, Theme.S(249));

            // 面板内容区（.pane-body{padding:4px 0}）
            PositionBody(_treePane, _tree);
            PositionBody(_deptPane, _deptList);
            PositionBody(_userPane, _userList);

            ClientSize = new Size(winW, top + Theme.S(440) + Theme.S(12));
        }

        private static void PositionBody(PermPane pane, Control body)
        {
            var r = pane.BodyRect;
            body.SetBounds(r.X, r.Y + Theme.S(4), Math.Max(4, r.Width - 1), Math.Max(4, r.Height - Theme.S(8) - 1));
        }

        // =====================================================================
        // 授权推导（与原型 JS 逻辑一致）
        // =====================================================================

        /// <summary>祖先部门一旦被授权，其下全部节点即被「覆盖」——整行置灰且不可单独操作。</summary>
        private bool Covered(PermNodeModel n)
        {
            var p = n.Parent;
            while (p != null)
            {
                if (p.IsDept && _deptOn.Contains(p.Key)) return true;
                p = p.Parent;
            }
            return false;
        }

        /// <summary>显示态：被覆盖或显式授权 → on；部门自下而上推导（下级全选＝on，部分＝mixed）。</summary>
        private string StateOf(PermNodeModel n)
        {
            if (Covered(n)) return "on";
            if (!n.IsDept) return _userOn.Contains(n.Key) ? "on" : "off";
            if (_deptOn.Contains(n.Key)) return "on";
            if (!n.HasChildren) return "off";
            var states = n.Children.Select(StateOf).ToList();
            if (states.All(s => s == "on")) return "on";
            return states.Any(s => s != "off") ? "mixed" : "off";
        }

        /// <summary>撤销该节点及其下全部授权（原型 permClearSubtree）。</summary>
        private void ClearSubtree(PermNodeModel node)
        {
            if (node.IsDept) _deptOn.Remove(node.Key);
            foreach (var c in node.Children)
            {
                if (c.IsDept) ClearSubtree(c);
                else _userOn.Remove(c.Key);
            }
        }

        private void Toggle(PermNodeModel node)
        {
            if (Covered(node)) return;   // 置灰行不可单独操作
            if (node.IsDept)
            {
                bool wasOn = StateOf(node) == "on";
                ClearSubtree(node);      // 先清掉下级零散授权
                if (!wasOn) _deptOn.Add(node.Key);   // 未勾选/半选 → 转为整部门授权
            }
            else
            {
                if (!_userOn.Remove(node.Key)) _userOn.Add(node.Key);
            }
            SyncDirty();
            RenderAll();
        }

        private void ToggleExpand(PermNodeModel node)
        {
            if (!_expanded.Remove(node.Key)) _expanded.Add(node.Key);
            _tree.Invalidate();
        }

        private void RemoveNode(PermNodeModel node)
        {
            if (node.IsDept) ClearSubtree(node);
            else _userOn.Remove(node.Key);
            SyncDirty();
            RenderAll();
        }

        /// <summary>右侧清单＝授权的「最上层」：被上层已授权节点包含的下级不再单独列出。</summary>
        private bool IsTop(PermNodeModel n)
        {
            var p = n.Parent;
            while (p != null)
            {
                if (StateOf(p) == "on") return false;
                p = p.Parent;
            }
            return true;
        }

        private string PathOf(PermNodeModel n)
        {
            var parts = new List<string>();
            var p = n.Parent;
            while (p != null) { parts.Insert(0, p.Name); p = p.Parent; }
            return string.Join(" / ", parts);
        }

        // =====================================================================
        // 搜索（与原型 permRows / permMatch / permRunSearch 一致）
        // =====================================================================

        private bool Match(PermNodeModel n) =>
            _query.Length > 0 &&
            ((n.Name != null && n.Name.IndexOf(_query, StringComparison.OrdinalIgnoreCase) >= 0) ||
             (n.Account != null && n.Account.IndexOf(_query, StringComparison.OrdinalIgnoreCase) >= 0));

        private bool HasMatchDesc(PermNodeModel n) => n.Children.Any(c => Match(c) || HasMatchDesc(c));

        private (int start, int len)? MatchRange(PermNodeModel n)
        {
            if (_query.Length == 0 || n.Name == null) return null;
            int i = n.Name.IndexOf(_query, StringComparison.OrdinalIgnoreCase);
            return i < 0 ? null : (i, _query.Length);
        }

        private List<PermRow> BuildRows()
        {
            var rows = new List<PermRow>();

            void PushSubtree(PermNodeModel n, int d)
            {
                rows.Add(new PermRow(n, d));
                foreach (var c in n.Children) PushSubtree(c, d + 1);
            }

            void Walk(PermNodeModel n, int d)
            {
                if (_query.Length == 0)
                {
                    rows.Add(new PermRow(n, d));
                    if (_expanded.Contains(n.Key))
                        foreach (var c in n.Children) Walk(c, d + 1);
                    return;
                }
                if (Match(n)) { PushSubtree(n, d); return; }
                if (HasMatchDesc(n))
                {
                    rows.Add(new PermRow(n, d));
                    foreach (var c in n.Children) Walk(c, d + 1);
                }
            }

            foreach (var r in _roots) Walk(r, 0);
            return rows;
        }

        private void RunSearch()
        {
            _query = (_searchBox.Inner.Text ?? "").Trim();
            _matches = _order.Where(Match).Select(n => n.Key).ToList();
            _cursor = _matches.Count > 0 ? 0 : -1;
            UpdateNav();
            _tree.SetCurrentKey(_cursor >= 0 ? _matches[_cursor] : null);
            _tree.Invalidate();
        }

        private void MoveCursor(int delta)
        {
            if (_matches.Count == 0) return;
            _cursor = (_cursor + delta + _matches.Count) % _matches.Count;
            UpdateNav();
            _tree.SetCurrentKey(_matches[_cursor]);
        }

        private void UpdateNav()
        {
            _countLabel.Text = _matches.Count > 0 ? $"{_cursor + 1}/{_matches.Count}" : "—";
            bool has = _matches.Count > 0;
            _prevBtn.Enabled = has;
            _nextBtn.Enabled = has;
        }

        // =====================================================================
        // 渲染联动
        // =====================================================================

        private bool IsDirty()
        {
            return !_deptOn.SetEquals(_savedDept) || !_userOn.SetEquals(_savedUser);
        }

        private void SyncDirty()
        {
            bool dirty = IsDirty();
            _dirtyFlag.Visible = dirty;
            _okFlag.Visible = false;                     // 与「权限已保存」互斥
            _resetBtn.Enabled = dirty && _loaded;
            _saveBtn.Enabled = dirty && _loaded && !_saving;
            _saveBtn.Text = "保存";
            _saveBtn.SetLoading(false);
        }

        private void RenderAll()
        {
            // 右侧清单＝最上层授权（State=on 且无上层 on 祖先）
            var shown = _order.Where(n => StateOf(n) == "on" && IsTop(n)).ToList();
            var depts = shown.Where(n => n.IsDept).ToList();
            var users = shown.Where(n => !n.IsDept).ToList();

            _deptItems = depts.Select(n => new SelItem(n, PathOf(n))).ToList();
            _userItems = users.Select(n => new SelItem(n, PathOf(n))).ToList();

            _deptPane.HeadCount = $"({depts.Count})";
            _userPane.HeadCount = $"({users.Count})";

            _tree.Invalidate();
            _deptPane.Invalidate();
            _userPane.Invalidate();
            _deptList.Invalidate();
            _userList.Invalidate();
        }

        // =====================================================================
        // 重置 / 保存（业务逻辑与原版一致）
        // =====================================================================

        private void ResetToSaved()
        {
            // 重置 = 回退到上次保存的状态
            _deptOn.Clear();
            foreach (var k in _savedDept) _deptOn.Add(k);
            _userOn.Clear();
            foreach (var k in _savedUser) _userOn.Add(k);
            SyncDirty();
            RenderAll();
            Logger.Info("已重置为上次保存的授权状态");
        }

        private async void SaveAsync()
        {
            if (_saving) return;
            _saving = true;
            _saveBtn.Enabled = false;
            _resetBtn.Enabled = false;
            _saveBtn.Text = "保存中";
            _saveBtn.SetLoading(true);

            bool ok = false;
            try
            {
                var userIdList = _userOn.Select(k => long.Parse(k.Substring(2))).ToList();
                var deptIdList = _deptOn.Select(k => long.Parse(k.Substring(2))).ToList();

                Logger.Info($"准备保存权限，部门数: {deptIdList.Count}，员工数: {userIdList.Count}");

                var requestData = new
                {
                    docId = _docId,
                    userIdList = userIdList,
                    deptIdList = deptIdList
                };

                var response = await _httpRequestService.PostAsync<object>(
                    ApiRoutes.DocAuthUpdate,
                    requestData,
                    token: GlobalState.Instance.Token
                );

                if (response != null && response.status == 200)
                {
                    ok = true;
                    // 保存成功 → 快照回写（原型 permSaveBtn：先 syncDirty 再亮「权限已保存」2.4s）
                    _savedDept = new HashSet<string>(_deptOn);
                    _savedUser = new HashSet<string>(_userOn);
                    SyncDirty();
                    _okFlag.Visible = true;
                    _okFlagTimer.Stop();
                    _okFlagTimer.Start();
                    Logger.Info("权限更新成功");
                }
                else
                {
                    Logger.Warning($"权限更新失败: {response?.message ?? "未知错误"}");
                    MessageBox.Show($"保存失败: {response?.message ?? "未知错误"}", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"保存权限时出错: {ex.Message}");
                MessageBox.Show($"保存失败: {ex.Message}", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally
            {
                _saving = false;
                if (ok)
                {
                    _saveBtn.Text = "保存";
                    _saveBtn.SetLoading(false);
                }
                else
                {
                    SyncDirty();   // 失败：按当前 dirty 状态恢复按钮可用性
                }
            }
        }

        // =====================================================================
        // 数据加载（业务逻辑与原版一致）
        // =====================================================================

        private async void LoadAuthTreeAsync()
        {
            try
            {
                _tree.ShowStatus = true;
                _tree.StatusIsError = false;
                _tree.StatusText = "正在加载权限树...";
                _tree.Invalidate();

                Logger.Info($"开始加载文档权限树，docId: {_docId}");
                var response = await _httpRequestService.GetAsync<LdapNodeDTO[]>(
                    ApiRoutes.DocAuthTree,
                    token: GlobalState.Instance.Token,
                    queryParams: new { docId = _docId }
                );

                if (response != null && response.data != null)
                {
                    Logger.Info("权限树数据加载成功");
                    BuildModel(response.data);
                    _loaded = true;
                    _tree.ShowStatus = false;   // 关闭加载提示，开始渲染组织树
                    _searchBox.Inner.Enabled = true;
                    _searchBtn.Enabled = true;
                    SyncDirty();
                    RenderAll();
                }
                else
                {
                    ShowLoadError("未获取到权限树数据");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"加载权限树时出错: {ex.Message}");
                ShowLoadError($"加载失败: {ex.Message}");
            }
        }

        private void ShowLoadError(string message)
        {
            _tree.ShowStatus = true;
            _tree.StatusIsError = true;
            _tree.StatusText = message;
            _tree.Invalidate();
        }

        private void BuildModel(LdapNodeDTO[] nodes)
        {
            _roots.Clear();
            _byId.Clear();
            _order.Clear();
            _deptOn.Clear();
            _userOn.Clear();
            _expanded.Clear();
            _matches = new List<string>();
            _cursor = -1;
            _query = "";

            PermNodeModel? Build(LdapNodeDTO dto, PermNodeModel? parent, int depth)
            {
                var m = new PermNodeModel
                {
                    Id = dto.id,
                    IsDept = dto.type == 0,
                    Name = dto.name ?? "",
                    Account = dto.account ?? "",
                    Dn = dto.dn ?? "",
                    HasAuth = dto.hasAuth,
                    Parent = parent,
                    Depth = depth
                };
                if (dto.deptList != null)
                    foreach (var c in dto.deptList)
                    {
                        var cm = Build(c, m, depth + 1);
                        if (cm != null) m.Children.Add(cm);
                    }
                if (dto.employList != null)
                    foreach (var c in dto.employList)
                    {
                        var cm = Build(c, m, depth + 1);
                        if (cm != null) m.Children.Add(cm);
                    }

                _byId[m.Key] = m;
                _order.Add(m);
                if (m.HasAuth)
                {
                    // 服务端既有授权 = 已保存状态（重置 / dirty 判定的基准）
                    if (m.IsDept) _deptOn.Add(m.Key);
                    else _userOn.Add(m.Key);
                }
                return m;
            }

            foreach (var n in nodes)
            {
                var m = Build(n, null, 0);
                if (m != null) _roots.Add(m);
            }

            // 默认展开根级 + 既有授权节点的全部祖先（便于直接看到已授权内容）
            foreach (var r in _roots) _expanded.Add(r.Key);
            foreach (var n in _order.Where(n => n.HasAuth))
            {
                var p = n.Parent;
                while (p != null) { _expanded.Add(p.Key); p = p.Parent; }
            }

            _savedDept = new HashSet<string>(_deptOn);
            _savedUser = new HashSet<string>(_userOn);
        }
    }

    /// <summary>
    /// LDAP 节点数据结构（接口 /doc/auth/tree 返回）。
    /// </summary>
    public class LdapNodeDTO
    {
        /// <summary>
        /// 节点 ID：type=0 为 sys_dept.id，type=1 为 sys_user.id
        /// </summary>
        public long id { get; set; }
        /// <summary>
        /// LDAP 完整路径（DN）：部门为 sys_dept.path；用户为 CN=账号,部门DN。
        /// 用于"勾选父部门按 dn.StartsWith() 包含子节点"的层级判断。
        /// </summary>
        public string dn { get; set; }
        public string name { get; set; }
        public int type { get; set; }
        public string account { get; set; }
        public bool hasAuth { get; set; }
        public LdapNodeDTO[] deptList { get; set; }
        public LdapNodeDTO[] employList { get; set; }
    }
}
