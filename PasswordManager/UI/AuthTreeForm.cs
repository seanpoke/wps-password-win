using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Linq;
using PasswordManager.Services.Request;
using PasswordManager.Services.Routing;
using PasswordManager.Utils;
using PasswordManager.UI.Controls;

namespace PasswordManager.UI
{
    public class AuthTreeForm : Form
    {
        public static bool IsOpen { get; private set; } = false;

        private Panel _topPanel;
        private TextBox _searchTextBox;
        private Button _searchButton;
        private AuthTreeView _authTreeView;
        private Panel _loadingPanel;
        private Label _loadingLabel;
        private Label _errorLabel;
        private HttpRequestService _httpRequestService;
        private string _docId;

        private Panel _leftPanel;
        private Panel _rightPanel;
        private Label _leftTitleLabel;

        private Label _deptTitleLabel;
        private Label _empTitleLabel;
        private ListBox _selectedDeptListBox;
        private ListBox _selectedEmpListBox;
        private Label _deptCountLabel;
        private Label _empCountLabel;

        private Button _confirmButton;
        private Button _cancelButton;
        private Button _saveButton;
        private Button _resetButton;

        private List<LdapNodeDTO> _selectedDepts = new List<LdapNodeDTO>();
        private List<LdapNodeDTO> _selectedEmps = new List<LdapNodeDTO>();
        private HashSet<string> _autoCheckedEmpDns = new HashSet<string>();
        private HashSet<string> _autoCheckedDeptDns = new HashSet<string>();
        private bool _isUpdatingCheckState = false;

        private Button _searchUpButton;
        private Button _searchDownButton;
        private Label _searchCountLabel;
        private List<TreeNode> _matchedNodes = new List<TreeNode>();
        private int _currentMatchIndex = -1;

        public AuthTreeForm(string docId)
        {
            _docId = docId;
            _httpRequestService = new HttpRequestService();
            InitializeComponent();
            IsOpen = true;
            LoadAuthTreeAsync();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            IsOpen = false;
        }

        private void InitializeComponent()
        {
            float dpiScale = DpiHelper.GetDpiScale();
            
            int windowWidth = (int)(700 * dpiScale);
            int windowHeight = (int)(580 * dpiScale);
            
            int buttonWidth = (int)(80 * dpiScale);
            int buttonHeight = (int)(30 * dpiScale);
            int buttonSpacing = (int)(10 * dpiScale);
            int bottomMargin = (int)(20 * dpiScale);
            
            this.Text = "文档权限";
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.ClientSize = new Size(windowWidth, windowHeight);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.White;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowIcon = false;
            this.TopMost = true;
            this.AutoScaleMode = AutoScaleMode.None;

            Font regularFont = new Font("微软雅黑", 9F);
            Font boldFont9 = new Font("微软雅黑", 9F, FontStyle.Bold);
            Font boldFont10 = new Font("微软雅黑", 10F, FontStyle.Bold);

            _topPanel = new Panel
            {
                Size = new Size(windowWidth, (int)(40 * dpiScale)),
                Location = new Point(0, 0),
                BackColor = Color.FromArgb(245, 245, 245),
                BorderStyle = BorderStyle.FixedSingle
            };

            _searchTextBox = new TextBox
            {
                Size = new Size((int)(180 * dpiScale), (int)(28 * dpiScale)),
                Location = new Point((int)(10 * dpiScale), (int)(6 * dpiScale)),
                Font = regularFont,
                PlaceholderText = "搜索部门或人员..."
            };
            _searchTextBox.KeyDown += SearchTextBox_KeyDown;

            _searchButton = new Button
            {
                Text = "搜索",
                Font = boldFont9,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(0, 120, 212),
                Size = new Size((int)(60 * dpiScale), (int)(28 * dpiScale)),
                Location = new Point((int)(195 * dpiScale), (int)(6 * dpiScale)),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TabStop = false
            };
            _searchButton.FlatAppearance.BorderSize = 0;
            _searchButton.Click += SearchButton_Click;
            _searchButton.MouseEnter += (sender, e) => _searchButton.BackColor = Color.FromArgb(0, 100, 180);
            _searchButton.MouseLeave += (sender, e) => _searchButton.BackColor = Color.FromArgb(0, 120, 212);

            _searchUpButton = new Button
            {
                Text = "◀",
                Font = boldFont10,
                ForeColor = Color.Black,
                BackColor = Color.White,
                Size = new Size((int)(28 * dpiScale), (int)(28 * dpiScale)),
                Location = new Point((int)(260 * dpiScale), (int)(6 * dpiScale)),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TabStop = false,
                Enabled = false
            };
            _searchUpButton.FlatAppearance.BorderSize = 0;
            _searchUpButton.Click += SearchUpButton_Click;

            _searchDownButton = new Button
            {
                Text = "▶",
                Font = boldFont10,
                ForeColor = Color.Black,
                BackColor = Color.White,
                Size = new Size((int)(28 * dpiScale), (int)(28 * dpiScale)),
                Location = new Point((int)(290 * dpiScale), (int)(6 * dpiScale)),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TabStop = false,
                Enabled = false
            };
            _searchDownButton.FlatAppearance.BorderSize = 0;
            _searchDownButton.Click += SearchDownButton_Click;

            _searchCountLabel = new Label
            {
                Text = "",
                Font = regularFont,
                ForeColor = Color.Gray,
                Size = new Size((int)(50 * dpiScale), (int)(28 * dpiScale)),
                Location = new Point((int)(322 * dpiScale), (int)(6 * dpiScale)),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _saveButton = new Button
            {
                Text = "保存",
                Font = boldFont10,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(156, 39, 176),
                Size = new Size((int)(65 * dpiScale), (int)(28 * dpiScale)),
                Location = new Point(windowWidth - (int)(70 * dpiScale) - (int)(75 * dpiScale) - (int)(15 * dpiScale), (int)(6 * dpiScale)),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TabStop = false
            };
            _saveButton.FlatAppearance.BorderSize = 0;
            _saveButton.Click += SaveButton_Click;
            _saveButton.MouseEnter += (sender, e) => _saveButton.BackColor = Color.FromArgb(136, 39, 156);
            _saveButton.MouseLeave += (sender, e) => _saveButton.BackColor = Color.FromArgb(156, 39, 176);

            _resetButton = new Button
            {
                Text = "重置",
                Font = boldFont10,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(156, 39, 176),
                Size = new Size((int)(65 * dpiScale), (int)(28 * dpiScale)),
                Location = new Point(windowWidth - (int)(70 * dpiScale) - (int)(75 * dpiScale) - (int)(85 * dpiScale) - (int)(15 * dpiScale), (int)(6 * dpiScale)),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TabStop = false
            };
            _resetButton.FlatAppearance.BorderSize = 0;
            _resetButton.Click += ResetButton_Click;
            _resetButton.MouseEnter += (sender, e) => _resetButton.BackColor = Color.FromArgb(136, 39, 156);
            _resetButton.MouseLeave += (sender, e) => _resetButton.BackColor = Color.FromArgb(156, 39, 176);

            _topPanel.Controls.Add(_searchTextBox);
            _topPanel.Controls.Add(_searchButton);
            _topPanel.Controls.Add(_searchUpButton);
            _topPanel.Controls.Add(_searchDownButton);
            _topPanel.Controls.Add(_searchCountLabel);
            _topPanel.Controls.Add(_resetButton);
            _topPanel.Controls.Add(_saveButton);

            int leftPanelWidth = (int)(330 * dpiScale);
            int rightPanelWidth = (int)(330 * dpiScale);
            int panelHeight = windowHeight - (int)(50 * dpiScale) - buttonHeight - bottomMargin - (int)(5 * dpiScale);
            int panelTop = (int)(45 * dpiScale);
            
            _leftPanel = new Panel
            {
                Size = new Size(leftPanelWidth, panelHeight),
                Location = new Point((int)(10 * dpiScale), panelTop),
                BorderStyle = BorderStyle.FixedSingle
            };

            _leftTitleLabel = new Label
            {
                Text = "选择部门或人员",
                Font = boldFont9,
                ForeColor = Color.Black,
                Size = new Size(leftPanelWidth, (int)(25 * dpiScale)),
                Location = new Point(0, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding((int)(5 * dpiScale), 0, 0, 0)
            };
            _leftPanel.Controls.Add(_leftTitleLabel);

            _authTreeView = new AuthTreeView
            {
                Size = new Size(leftPanelWidth - 4, panelHeight - (int)(30 * dpiScale)),
                Location = new Point(2, (int)(25 * dpiScale)),
                ShowLines = true,
                ShowPlusMinus = true,
                ShowRootLines = true,
                Font = regularFont,
                LineColor = Color.LightGray,
                CheckBoxes = true
            };
            _authTreeView.AfterCheck += AuthTreeView_AfterCheck;
            _authTreeView.BeforeCheck += AuthTreeView_BeforeCheck;
            _leftPanel.Controls.Add(_authTreeView);

            _rightPanel = new Panel
            {
                Size = new Size(rightPanelWidth, panelHeight),
                Location = new Point((int)(10 * dpiScale) + leftPanelWidth + (int)(10 * dpiScale), panelTop),
                BorderStyle = BorderStyle.FixedSingle
            };

            _deptTitleLabel = new Label
            {
                Text = "已选择的部门",
                Font = boldFont9,
                ForeColor = Color.Black,
                Size = new Size(rightPanelWidth - (int)(50 * dpiScale), (int)(25 * dpiScale)),
                Location = new Point(0, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding((int)(5 * dpiScale), 0, 0, 0)
            };

            _deptCountLabel = new Label
            {
                Text = "",
                Font = regularFont,
                ForeColor = Color.Gray,
                Size = new Size((int)(50 * dpiScale), (int)(25 * dpiScale)),
                Location = new Point(rightPanelWidth - (int)(55 * dpiScale), 0),
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, (int)(5 * dpiScale), 0)
            };

            _selectedDeptListBox = new ListBox
            {
                Size = new Size(rightPanelWidth - (int)(25 * dpiScale), (int)(160 * dpiScale)),
                Location = new Point(2, (int)(25 * dpiScale)),
                Font = regularFont,
                BorderStyle = BorderStyle.None,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = (int)(22 * dpiScale)
            };
            _selectedDeptListBox.DrawItem += SelectedDeptListBox_DrawItem;
            _selectedDeptListBox.MouseClick += SelectedListBox_MouseClick;

            _empTitleLabel = new Label
            {
                Text = "已选择的员工",
                Font = boldFont9,
                ForeColor = Color.Black,
                Size = new Size(rightPanelWidth - (int)(50 * dpiScale), (int)(25 * dpiScale)),
                Location = new Point(0, (int)(190 * dpiScale)),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding((int)(5 * dpiScale), 0, 0, 0)
            };

            _empCountLabel = new Label
            {
                Text = "",
                Font = regularFont,
                ForeColor = Color.Gray,
                Size = new Size((int)(50 * dpiScale), (int)(25 * dpiScale)),
                Location = new Point(rightPanelWidth - (int)(55 * dpiScale), (int)(190 * dpiScale)),
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, (int)(5 * dpiScale), 0)
            };

            int deptSectionHeight = (int)(190 * dpiScale);
            int empListTop = (int)(215 * dpiScale);
            
            _selectedEmpListBox = new ListBox
            {
                Size = new Size(rightPanelWidth - (int)(25 * dpiScale), panelHeight - empListTop),
                Location = new Point(2, empListTop),
                Font = regularFont,
                BorderStyle = BorderStyle.None,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = (int)(22 * dpiScale)
            };
            _selectedEmpListBox.DrawItem += SelectedEmpListBox_DrawItem;
            _selectedEmpListBox.MouseClick += SelectedListBox_MouseClick;

            _rightPanel.Controls.Add(_deptTitleLabel);
            _rightPanel.Controls.Add(_deptCountLabel);
            _rightPanel.Controls.Add(_selectedDeptListBox);
            _rightPanel.Controls.Add(_empTitleLabel);
            _rightPanel.Controls.Add(_empCountLabel);
            _rightPanel.Controls.Add(_selectedEmpListBox);

            _confirmButton = new Button
            {
                Text = "确定",
                Font = boldFont9,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(0, 120, 212),
                Size = new Size(buttonWidth, buttonHeight),
                Location = new Point(windowWidth - buttonWidth * 2 - buttonSpacing - bottomMargin - (int)(10 * dpiScale), windowHeight - buttonHeight - bottomMargin),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _confirmButton.FlatAppearance.BorderSize = 0;
            _confirmButton.Click += ConfirmButton_Click;

            _cancelButton = new Button
            {
                Text = "取消",
                Font = boldFont9,
                ForeColor = Color.Black,
                BackColor = Color.FromArgb(238, 238, 238),
                Size = new Size(buttonWidth, buttonHeight),
                Location = new Point(windowWidth - buttonWidth - bottomMargin - (int)(10 * dpiScale), windowHeight - buttonHeight - bottomMargin),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _cancelButton.FlatAppearance.BorderSize = 0;
            _cancelButton.Click += (sender, e) => this.Close();

            _loadingPanel = new Panel
            {
                Size = new Size(windowWidth, panelHeight + (int)(5 * dpiScale)),
                Location = new Point(0, panelTop),
                BackColor = Color.White
            };

            _loadingLabel = new Label
            {
                Text = "正在加载权限树...",
                Font = new Font("微软雅黑", 10F * dpiScale),
                ForeColor = Color.Gray,
                Size = new Size(windowWidth, (int)(20 * dpiScale)),
                Location = new Point(0, (panelHeight - (int)(20 * dpiScale)) / 2),
                TextAlign = ContentAlignment.MiddleCenter
            };
            _loadingPanel.Controls.Add(_loadingLabel);

            _errorLabel = new Label
            {
                Text = "",
                Font = new Font("微软雅黑", 10F * dpiScale),
                ForeColor = Color.Red,
                Size = new Size(windowWidth, (int)(40 * dpiScale)),
                Location = new Point(0, (panelHeight - (int)(40 * dpiScale)) / 2),
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false
            };
            _loadingPanel.Controls.Add(_errorLabel);

            this.Controls.Add(_topPanel);
            this.Controls.Add(_loadingPanel);

            this.Controls.Add(_leftPanel);
            this.Controls.Add(_rightPanel);
            this.Controls.Add(_confirmButton);
            this.Controls.Add(_cancelButton);

            _leftPanel.Visible = false;
            _rightPanel.Visible = false;
            _confirmButton.Visible = false;
            _cancelButton.Visible = false;
            _saveButton.Visible = false;
            _resetButton.Visible = false;
        }

        private void AuthTreeView_BeforeCheck(object sender, TreeViewCancelEventArgs e)
        {
            var nodeDto = e.Node.Tag as LdapNodeDTO;
            if (nodeDto != null)
            {
                if (e.Node.Checked && nodeDto.type == 1 && _autoCheckedEmpDns.Contains(nodeDto.dn))
                {
                    e.Cancel = true;
                }
                else if (e.Node.Checked && nodeDto.type == 0 && _autoCheckedDeptDns.Contains(nodeDto.dn))
                {
                    e.Cancel = true;
                }
            }
        }

        private void AuthTreeView_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (_isUpdatingCheckState) return;

            var node = e.Node.Tag as LdapNodeDTO;
            if (node == null) return;

            if (node.type == 0)
            {
                HandleDeptNodeCheck(e.Node, e.Node.Checked);
            }
            else
            {
                HandleEmpNodeCheck(e.Node, e.Node.Checked);
            }
            UpdateSelectedCount();
        }

        private void HandleDeptNodeCheck(TreeNode deptNode, bool isChecked)
        {
            var deptDto = deptNode.Tag as LdapNodeDTO;
            if (deptDto == null) return;

            _isUpdatingCheckState = true;
            try
            {
                if (isChecked)
                {
                    deptNode.Checked = true;
                    
                    string deptDn = deptDto.dn;
                    
                    _selectedEmps.RemoveAll(emp => emp.dn.StartsWith(deptDn));
                    _selectedDepts.RemoveAll(d => d.dn.StartsWith(deptDn) && d.dn != deptDn);
                    
                    AutoCheckChildEmps(deptNode, true);
                    
                    if (!_selectedDepts.Exists(n => n.dn == deptDto.dn))
                    {
                        _selectedDepts.Add(deptDto);
                    }
                }
                else
                {
                    deptNode.Checked = false;
                    AutoUncheckChildEmps(deptNode);
                    _selectedDepts.RemoveAll(n => n.dn == deptDto.dn);
                }
            }
            finally
            {
                _isUpdatingCheckState = false;
            }
            UpdateSelectedListBoxes();
        }

        private void HandleEmpNodeCheck(TreeNode empNode, bool isChecked)
        {
            var empDto = empNode.Tag as LdapNodeDTO;
            if (empDto == null) return;

            if (_autoCheckedEmpDns.Contains(empDto.dn))
            {
                if (!isChecked)
                {
                    _autoCheckedEmpDns.Remove(empDto.dn);
                    bool isMatched = _matchedNodes.Contains(empNode);
                    empNode.ForeColor = isMatched ? Color.Red : Color.Black;
                }
                else
                {
                    return;
                }
            }

            if (isChecked)
            {
                if (!_selectedEmps.Exists(n => n.dn == empDto.dn))
                {
                    _selectedEmps.Add(empDto);
                }
            }
            else
            {
                _selectedEmps.RemoveAll(n => n.dn == empDto.dn);
            }
            UpdateSelectedListBoxes();
        }

        private void AutoCheckChildEmps(TreeNode parentNode, bool check)
        {
            foreach (TreeNode childNode in parentNode.Nodes)
            {
                var childDto = childNode.Tag as LdapNodeDTO;
                if (childDto != null)
                {
                    if (childDto.type == 1)
                    {
                        if (check)
                        {
                            childNode.Checked = true;
                            
                            if (!_autoCheckedEmpDns.Contains(childDto.dn))
                            {
                                _autoCheckedEmpDns.Add(childDto.dn);
                            }
                            bool isMatched = _matchedNodes.Contains(childNode);
                            childNode.ForeColor = isMatched ? Color.Red : Color.Gray;
                            
                            _selectedEmps.RemoveAll(n => n.dn == childDto.dn);
                        }
                    }
                    else
                    {
                        if (check)
                        {
                            childNode.Checked = true;
                            
                            if (!_autoCheckedDeptDns.Contains(childDto.dn))
                            {
                                _autoCheckedDeptDns.Add(childDto.dn);
                            }
                            bool isMatched = _matchedNodes.Contains(childNode);
                            childNode.ForeColor = isMatched ? Color.Red : Color.Gray;
                            
                            _selectedDepts.RemoveAll(n => n.dn == childDto.dn);
                            _selectedEmps.RemoveAll(emp => emp.dn.StartsWith(childDto.dn));
                        }
                    }
                }
                AutoCheckChildEmps(childNode, check);
            }
        }

        private void AutoUncheckChildEmps(TreeNode parentNode)
        {
            foreach (TreeNode childNode in parentNode.Nodes)
            {
                var childDto = childNode.Tag as LdapNodeDTO;
                if (childDto != null)
                {
                    if (childDto.type == 1)
                    {
                        if (_autoCheckedEmpDns.Contains(childDto.dn))
                        {
                            _autoCheckedEmpDns.Remove(childDto.dn);
                            bool isMatched = _matchedNodes.Contains(childNode);
                            childNode.ForeColor = isMatched ? Color.Red : Color.Black;
                            
                            bool isManuallyChecked = _selectedEmps.Exists(n => n.dn == childDto.dn);
                            
                            if (!IsParentDeptChecked(childNode.Parent) && !isManuallyChecked)
                            {
                                childNode.Checked = false;
                            }
                        }
                    }
                    else
                    {
                        if (_autoCheckedDeptDns.Contains(childDto.dn))
                        {
                            _autoCheckedDeptDns.Remove(childDto.dn);
                            bool isMatched = _matchedNodes.Contains(childNode);
                            childNode.ForeColor = isMatched ? Color.Red : Color.Black;
                            
                            bool isManuallyChecked = _selectedDepts.Exists(n => n.dn == childDto.dn);
                            
                            if (!IsParentDeptChecked(childNode.Parent) && !isManuallyChecked)
                            {
                                childNode.Checked = false;
                            }
                        }
                    }
                }
                AutoUncheckChildEmps(childNode);
            }
        }

        private bool IsParentDeptChecked(TreeNode node)
        {
            if (node == null) return false;
            
            var nodeDto = node.Tag as LdapNodeDTO;
            if (nodeDto != null && nodeDto.type == 0 && node.Checked)
            {
                if (_autoCheckedDeptDns.Contains(nodeDto.dn))
                {
                    return IsParentDeptChecked(node.Parent);
                }
                return true;
            }
            
            return IsParentDeptChecked(node.Parent);
        }

        private void UpdateParentCheckState(TreeNode parentNode)
        {
            if (parentNode == null) return;

            var parentDto = parentNode.Tag as LdapNodeDTO;
            if (parentDto == null || parentDto.type == 1) return;

            int checkedCount = 0;
            int totalCount = 0;

            foreach (TreeNode childNode in parentNode.Nodes)
            {
                var childDto = childNode.Tag as LdapNodeDTO;
                if (childDto != null && childDto.type == 1)
                {
                    totalCount++;
                    if (childNode.Checked)
                    {
                        checkedCount++;
                    }
                }
            }

            _isUpdatingCheckState = true;
            try
            {
                if (checkedCount == 0)
                {
                    parentNode.Checked = false;
                    _selectedDepts.RemoveAll(n => n.dn == parentDto.dn);
                }
                else if (checkedCount == totalCount)
                {
                    parentNode.Checked = true;
                    if (!_selectedDepts.Exists(n => n.dn == parentDto.dn))
                    {
                        _selectedDepts.Add(parentDto);
                    }
                }
                else
                {
                    parentNode.Checked = false;
                }
            }
            finally
            {
                _isUpdatingCheckState = false;
            }

            UpdateParentCheckState(parentNode.Parent);
        }

        private void UpdateSelectedListBoxes()
        {
            _selectedDeptListBox.Items.Clear();
            foreach (var dept in _selectedDepts)
            {
                string displayText = $"[{dept.name}]";
                _selectedDeptListBox.Items.Add(new { Dn = dept.dn, DisplayText = displayText, Node = dept, IsDept = true });
            }

            _selectedEmpListBox.Items.Clear();
            foreach (var emp in _selectedEmps)
            {
                string displayText = $"{emp.name} ({emp.account ?? ""})";
                _selectedEmpListBox.Items.Add(new { Dn = emp.dn, DisplayText = displayText, Node = emp, IsDept = false });
            }
            UpdateSelectedCount();
        }

        private void UpdateSelectedCount()
        {
            _deptCountLabel.Text = $"({_selectedDepts.Count})";
            _empCountLabel.Text = $"({_selectedEmps.Count})";
        }

        private void SelectedDeptListBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            var item = _selectedDeptListBox.Items[e.Index] as dynamic;
            string displayText = item.DisplayText;

            e.DrawBackground();

            int paddingLeft = 5;
            int paddingTop = 2;
            int xOffset = 18;

            using (Brush textBrush = new SolidBrush(Color.Black))
            {
                e.Graphics.DrawString(displayText, e.Font, textBrush, e.Bounds.X + paddingLeft, e.Bounds.Y + paddingTop);
            }

            using (Brush xBrush = new SolidBrush(Color.Gray))
            {
                Font xFont = new Font("微软雅黑", 10F, FontStyle.Bold);
                e.Graphics.DrawString("×", xFont, xBrush, e.Bounds.Right - xOffset, e.Bounds.Y + paddingTop);
            }

            e.DrawFocusRectangle();
        }

        private void SelectedEmpListBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            var item = _selectedEmpListBox.Items[e.Index] as dynamic;
            string displayText = item.DisplayText;

            e.DrawBackground();

            int paddingLeft = 5;
            int paddingTop = 2;
            int xOffset = 18;

            using (Brush textBrush = new SolidBrush(Color.Black))
            {
                e.Graphics.DrawString(displayText, e.Font, textBrush, e.Bounds.X + paddingLeft, e.Bounds.Y + paddingTop);
            }

            using (Brush xBrush = new SolidBrush(Color.Gray))
            {
                Font xFont = new Font("微软雅黑", 10F, FontStyle.Bold);
                e.Graphics.DrawString("×", xFont, xBrush, e.Bounds.Right - xOffset, e.Bounds.Y + paddingTop);
            }

            e.DrawFocusRectangle();
        }

        private void SelectedListBox_MouseClick(object sender, MouseEventArgs e)
        {
            ListBox listBox = sender as ListBox;
            if (listBox == null) return;

            int index = listBox.IndexFromPoint(e.Location);
            if (index < 0) return;

            var item = listBox.Items[index] as dynamic;
            if (item == null) return;

            int itemWidth = listBox.GetItemRectangle(index).Width;
            int deleteButtonWidth = 20;
            if (e.X > itemWidth - deleteButtonWidth)
            {
                LdapNodeDTO node = item.Node;
                bool isDept = item.IsDept;

                if (isDept)
                {
                    TreeNode treeNode = FindTreeNodeByDn(_authTreeView.Nodes, node.dn);
                    if (treeNode != null)
                    {
                        HandleDeptNodeCheck(treeNode, false);
                    }
                }
                else
                {
                    TreeNode treeNode = FindTreeNodeByDn(_authTreeView.Nodes, node.dn);
                    if (treeNode != null)
                    {
                        _isUpdatingCheckState = true;
                        try
                        {
                            treeNode.Checked = false;
                        }
                        finally
                        {
                            _isUpdatingCheckState = false;
                        }
                        HandleEmpNodeCheck(treeNode, false);
                    }
                }
            }
        }

        private TreeNode FindTreeNodeByDn(TreeNodeCollection nodes, string dn)
        {
            foreach (TreeNode node in nodes)
            {
                var nodeDto = node.Tag as LdapNodeDTO;
                if (nodeDto != null && nodeDto.dn == dn)
                {
                    return node;
                }
                TreeNode found = FindTreeNodeByDn(node.Nodes, dn);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        private async void ConfirmButton_Click(object sender, EventArgs e)
        {
            try
            {
                _confirmButton.Enabled = false;
                
                var accountDnList = _selectedEmps.Select(emp => emp.dn).ToList();
                var deptDnList = _selectedDepts.Select(dept => dept.dn).ToList();

                Logger.Info($"准备保存权限，部门数: {deptDnList.Count}，员工数: {accountDnList.Count}");

                var requestData = new
                {
                    docId = _docId,
                    accountDnList = accountDnList,
                    deptDnList = deptDnList
                };

                var response = await _httpRequestService.PostAsync<object>(
                    ApiRoutes.DocAuthUpdate,
                    requestData,
                    token: GlobalState.Instance.Token
                );

                if (response != null && response.status == 200)
                {
                    Logger.Info("权限更新成功");
                    ShowNotification("权限保存成功");
                    this.Close();
                }
                else
                {
                    Logger.Warning($"权限更新失败: {response?.message ?? "未知错误"}");
                    ShowNotification($"保存失败: {response?.message ?? "未知错误"}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"保存权限时出错: {ex.Message}");
                ShowNotification($"保存失败: {ex.Message}");
            }
            finally
            {
                _confirmButton.Enabled = true;
            }
        }

        private void ResetButton_Click(object sender, EventArgs e)
        {
            try
            {
                _autoCheckedEmpDns.Clear();
                _autoCheckedDeptDns.Clear();

                _isUpdatingCheckState = true;
                foreach (TreeNode node in _authTreeView.Nodes)
                {
                    UncheckAllNodes(node);
                }
                _isUpdatingCheckState = false;

                _selectedDepts.Clear();
                _selectedEmps.Clear();

                UpdateSelectedListBoxes();

                Logger.Info("已重置所有选择的部门和员工");
                ShowNotification("已重置所有选择项");
            }
            catch (Exception ex)
            {
                Logger.Error($"重置操作失败: {ex.Message}");
                ShowNotification("重置失败");
            }
        }

        private void UncheckAllNodes(TreeNode parentNode)
        {
            parentNode.Checked = false;
            parentNode.ForeColor = Color.Black;

            foreach (TreeNode childNode in parentNode.Nodes)
            {
                UncheckAllNodes(childNode);
            }
        }

        private async void SaveButton_Click(object sender, EventArgs e)
        {
            try
            {
                _saveButton.Enabled = false;

                var accountDnList = _selectedEmps.Select(emp => emp.dn).ToList();
                var deptDnList = _selectedDepts.Select(dept => dept.dn).ToList();

                Logger.Info($"准备保存权限，部门数: {deptDnList.Count}，员工数: {accountDnList.Count}");

                var requestData = new
                {
                    docId = _docId,
                    accountDnList = accountDnList,
                    deptDnList = deptDnList
                };

                var response = await _httpRequestService.PostAsync<object>(
                    ApiRoutes.DocAuthUpdate,
                    requestData,
                    token: GlobalState.Instance.Token
                );

                if (response != null && response.status == 200)
                {
                    Logger.Info("权限更新成功");
                    ShowNotification("权限保存成功");
                }
                else
                {
                    Logger.Warning($"权限更新失败: {response?.message ?? "未知错误"}");
                    ShowNotification($"保存失败: {response?.message ?? "未知错误"}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"保存权限时出错: {ex.Message}");
                ShowNotification($"保存失败: {ex.Message}");
            }
            finally
            {
                _saveButton.Enabled = true;
            }
        }

        private void ShowNotification(string message)
        {
            MessageBox.Show(message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async void LoadAuthTreeAsync()
        {
            try
            {
                _errorLabel.Visible = false;
                _loadingLabel.Text = "正在加载权限树...";
                _loadingLabel.Visible = true;

                Logger.Info($"开始加载文档权限树，docId: {_docId}");
                var response = await _httpRequestService.GetAsync<LdapNodeDTO[]>(
                    ApiRoutes.DocAuthTree,
                    token: GlobalState.Instance.Token,
                    queryParams: new { docId = _docId }
                );

                if (response != null && response.data != null)
                {
                    Logger.Info("权限树数据加载成功");
                    PopulateTreeView(response.data);
                    _loadingPanel.Visible = false;
                    _leftPanel.Visible = true;
                    _rightPanel.Visible = true;
                    _confirmButton.Visible = true;
                    _cancelButton.Visible = true;
                    _saveButton.Visible = true;
                    _resetButton.Visible = true;
                }
                else
                {
                    ShowError("未获取到权限树数据");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"加载权限树时出错: {ex.Message}");
                ShowError($"加载失败: {ex.Message}");
            }
        }

        private void ShowError(string message)
        {
            _loadingLabel.Visible = false;
            _errorLabel.Text = message;
            _errorLabel.Visible = true;
        }

        private void PopulateTreeView(LdapNodeDTO[] nodes)
        {
            _authTreeView.Nodes.Clear();
            _selectedDepts.Clear();
            _selectedEmps.Clear();
            _autoCheckedEmpDns.Clear();
            _autoCheckedDeptDns.Clear();

            foreach (var node in nodes)
            {
                var treeNode = CreateTreeNode(node);
                _authTreeView.Nodes.Add(treeNode);
            }

            UpdateSelectedListBoxes();
        }

        private TreeNode CreateTreeNode(LdapNodeDTO node)
        {
            string nodeText = node.type == 0
                ? $"[{node.name}]"
                : $"{node.name} ({node.account ?? ""})";

            var treeNode = new TreeNode(nodeText)
            {
                Tag = node,
                Checked = node.hasAuth
            };

            if (node.deptList != null && node.deptList.Length > 0)
            {
                foreach (var dept in node.deptList)
                {
                    treeNode.Nodes.Add(CreateTreeNode(dept));
                }
            }

            if (node.employList != null && node.employList.Length > 0)
            {
                foreach (var employ in node.employList)
                {
                    var empNode = CreateTreeNode(employ);
                    treeNode.Nodes.Add(empNode);
                }
            }

            if (node.hasAuth)
            {
                if (node.type == 0)
                {
                    _selectedDepts.Add(node);
                    AutoCheckChildEmps(treeNode, true);
                }
                else
                {
                    _selectedEmps.Add(node);
                }
            }

            return treeNode;
        }

        private void SearchButton_Click(object sender, EventArgs e)
        {
            PerformSearch();
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                PerformSearch();
            }
        }

        private void PerformSearch()
        {
            string searchText = _searchTextBox.Text.Trim();
            
            if (string.IsNullOrEmpty(searchText))
            {
                ResetSearchHighlight();
                return;
            }

            _matchedNodes.Clear();
            _currentMatchIndex = -1;

            bool firstMatchFound = false;
            HighlightMatchingNodes(_authTreeView.Nodes, searchText, false, ref firstMatchFound);

            UpdateSearchNavigation();
        }

        private void HighlightMatchingNodes(TreeNodeCollection nodes, string searchText, bool parentMatched)
        {
            bool firstMatchFound = false;
            HighlightMatchingNodes(nodes, searchText, parentMatched, ref firstMatchFound);
        }

        private void HighlightMatchingNodes(TreeNodeCollection nodes, string searchText, bool parentMatched, ref bool firstMatchFound)
        {
            foreach (TreeNode node in nodes)
            {
                var nodeDto = node.Tag as LdapNodeDTO;
                bool isMatch = false;

                if (nodeDto != null)
                {
                    if (!string.IsNullOrEmpty(nodeDto.name) && 
                        nodeDto.name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        isMatch = true;
                    }
                    else if (!string.IsNullOrEmpty(nodeDto.account) && 
                             nodeDto.account.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        isMatch = true;
                    }
                }

                bool shouldShow = isMatch || parentMatched;
                
                if (shouldShow)
                {
                    if (isMatch)
                    {
                        node.ForeColor = Color.Red;
                        _matchedNodes.Add(node);
                    }
                    else
                    {
                        bool isAutoChecked = _autoCheckedEmpDns.Contains(nodeDto?.dn) || 
                                             _autoCheckedDeptDns.Contains(nodeDto?.dn);
                        node.ForeColor = isAutoChecked ? Color.Gray : Color.Black;
                    }
                    
                    if (isMatch && !firstMatchFound)
                    {
                        node.EnsureVisible();
                        firstMatchFound = true;
                    }
                    else if (!isMatch)
                    {
                        node.EnsureVisible();
                    }
                }
                else
                {
                    bool isAutoChecked = _autoCheckedEmpDns.Contains(nodeDto?.dn) || 
                                         _autoCheckedDeptDns.Contains(nodeDto?.dn);
                    
                    node.ForeColor = isAutoChecked ? Color.Gray : Color.Black;
                }

                HighlightMatchingNodes(node.Nodes, searchText, shouldShow, ref firstMatchFound);
            }
        }

        private void ResetSearchHighlight()
        {
            ResetHighlight(_authTreeView.Nodes);
            _matchedNodes.Clear();
            _currentMatchIndex = -1;
            UpdateSearchNavigation();
        }

        private void ResetHighlight(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                node.ForeColor = _autoCheckedEmpDns.Contains(((LdapNodeDTO)node.Tag)?.dn) || 
                               _autoCheckedDeptDns.Contains(((LdapNodeDTO)node.Tag)?.dn) 
                               ? Color.Gray : Color.Black;
                ResetHighlight(node.Nodes);
            }
        }

        private void UpdateSearchNavigation()
        {
            if (_matchedNodes.Count == 0)
            {
                _searchCountLabel.Text = "";
                _searchUpButton.Enabled = false;
                _searchDownButton.Enabled = false;
                _searchUpButton.Visible = false;
                _searchDownButton.Visible = false;
                _searchCountLabel.Visible = false;
            }
            else
            {
                if (_currentMatchIndex < 0)
                {
                    _currentMatchIndex = 0;
                }
                _searchCountLabel.Text = $"{_currentMatchIndex + 1}/{_matchedNodes.Count}";
                _searchUpButton.Enabled = _matchedNodes.Count > 1;
                _searchDownButton.Enabled = _matchedNodes.Count > 1;
                _searchUpButton.Visible = true;
                _searchDownButton.Visible = true;
                _searchCountLabel.Visible = true;
            }
        }

        private void SearchUpButton_Click(object sender, EventArgs e)
        {
            if (_matchedNodes.Count == 0) return;
            
            _currentMatchIndex--;
            if (_currentMatchIndex < 0)
            {
                _currentMatchIndex = _matchedNodes.Count - 1;
            }
            
            NavigateToMatch();
        }

        private void SearchDownButton_Click(object sender, EventArgs e)
        {
            if (_matchedNodes.Count == 0) return;
            
            _currentMatchIndex++;
            if (_currentMatchIndex >= _matchedNodes.Count)
            {
                _currentMatchIndex = 0;
            }
            
            NavigateToMatch();
        }

        private void NavigateToMatch()
        {
            if (_matchedNodes.Count > 0 && _currentMatchIndex >= 0 && _currentMatchIndex < _matchedNodes.Count)
            {
                TreeNode node = _matchedNodes[_currentMatchIndex];
                node.TreeView.TopNode = node;
                node.TreeView.SelectedNode = node;
                node.EnsureVisible();
                _searchCountLabel.Text = $"{_currentMatchIndex + 1}/{_matchedNodes.Count}";
            }
        }
    }

    public class LdapNodeDTO
    {
        public string dn { get; set; }
        public string name { get; set; }
        public int type { get; set; }
        public string account { get; set; }
        public bool hasAuth { get; set; }
        public LdapNodeDTO[] deptList { get; set; }
        public LdapNodeDTO[] employList { get; set; }
    }
}