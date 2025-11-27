// Xbox Full Screen Experience Tool
// Copyright (C) 2025 8bit2qubit

// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.

// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

namespace XboxFullScreenExperienceTool
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            btnEnable = new Button();
            btnDisable = new Button();
            grpActions = new GroupBox();
            chkStartKeyboardOnLogon = new CheckBox();
            btnCheckUpdates = new Button();
            btnOpenSettings = new Button();
            grpOutput = new GroupBox();
            txtOutput = new RichTextBox();
            lblStatus = new Label();
            grpPhysPanel = new GroupBox();
            radPhysPanelDrv = new RadioButton();
            radPhysPanelCS = new RadioButton();
            toolTip = new ToolTip(components);
            cboLanguage = new ComboBox();
            grpActions.SuspendLayout();
            grpOutput.SuspendLayout();
            grpPhysPanel.SuspendLayout();
            SuspendLayout();
            // 
            // btnEnable
            // 
            btnEnable.Font = new Font("Microsoft JhengHei UI", 12F, FontStyle.Bold, GraphicsUnit.Point, 136);
            btnEnable.ForeColor = Color.DarkGreen;
            btnEnable.Location = new Point(28, 19);
            btnEnable.Margin = new Padding(4, 5, 4, 5);
            btnEnable.Name = "btnEnable";
            btnEnable.Size = new Size(423, 66);
            btnEnable.TabIndex = 6;
            btnEnable.Text = "啟用 Xbox 全螢幕體驗";
            btnEnable.UseVisualStyleBackColor = true;
            btnEnable.Click += btnEnable_Click;
            // 
            // btnDisable
            // 
            btnDisable.Font = new Font("Microsoft JhengHei UI", 12F, FontStyle.Bold, GraphicsUnit.Point, 136);
            btnDisable.ForeColor = Color.DarkRed;
            btnDisable.Location = new Point(477, 19);
            btnDisable.Margin = new Padding(4, 5, 4, 5);
            btnDisable.Name = "btnDisable";
            btnDisable.Size = new Size(424, 66);
            btnDisable.TabIndex = 7;
            btnDisable.Text = "停用並還原";
            btnDisable.UseVisualStyleBackColor = true;
            btnDisable.Click += btnDisable_Click;
            // 
            // grpActions
            // 
            grpActions.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            grpActions.Controls.Add(chkStartKeyboardOnLogon);
            grpActions.Controls.Add(btnDisable);
            grpActions.Controls.Add(btnEnable);
            grpActions.Controls.Add(btnCheckUpdates);
            grpActions.Controls.Add(btnOpenSettings);
            grpActions.Location = new Point(18, 86);
            grpActions.Margin = new Padding(4, 5, 4, 5);
            grpActions.Name = "grpActions";
            grpActions.Padding = new Padding(4, 5, 4, 5);
            grpActions.Size = new Size(928, 174);
            grpActions.TabIndex = 5;
            grpActions.TabStop = false;
            grpActions.Text = "操作";
            // 
            // chkStartKeyboardOnLogon
            // 
            chkStartKeyboardOnLogon.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            chkStartKeyboardOnLogon.AutoSize = true;
            chkStartKeyboardOnLogon.Location = new Point(28, 139);
            chkStartKeyboardOnLogon.Margin = new Padding(4, 5, 4, 5);
            chkStartKeyboardOnLogon.Name = "chkStartKeyboardOnLogon";
            chkStartKeyboardOnLogon.Size = new Size(376, 23);
            chkStartKeyboardOnLogon.TabIndex = 10;
            chkStartKeyboardOnLogon.Text = "在登入時啟動遊戲控制器鍵盤，並自動收至背景待命";
            chkStartKeyboardOnLogon.UseVisualStyleBackColor = true;
            chkStartKeyboardOnLogon.CheckedChanged += chkStartKeyboardOnLogon_CheckedChanged;
            // 
            // btnCheckUpdates
            // 
            btnCheckUpdates.Font = new Font("Microsoft JhengHei UI", 10F, FontStyle.Regular, GraphicsUnit.Point, 136);
            btnCheckUpdates.Location = new Point(477, 95);
            btnCheckUpdates.Name = "btnCheckUpdates";
            btnCheckUpdates.Size = new Size(424, 35);
            btnCheckUpdates.TabIndex = 9;
            btnCheckUpdates.Text = "檢查 MS Store 的 Xbox 更新";
            btnCheckUpdates.UseVisualStyleBackColor = true;
            btnCheckUpdates.Click += btnCheckUpdates_Click;
            // 
            // btnOpenSettings
            // 
            btnOpenSettings.Enabled = false;
            btnOpenSettings.Font = new Font("Microsoft JhengHei UI", 10F, FontStyle.Regular, GraphicsUnit.Point, 136);
            btnOpenSettings.Location = new Point(28, 95);
            btnOpenSettings.Name = "btnOpenSettings";
            btnOpenSettings.Size = new Size(423, 35);
            btnOpenSettings.TabIndex = 8;
            btnOpenSettings.Text = "開啟全螢幕體驗設定";
            btnOpenSettings.UseVisualStyleBackColor = true;
            btnOpenSettings.Click += btnOpenSettings_Click;
            // 
            // grpOutput
            // 
            grpOutput.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            grpOutput.Controls.Add(txtOutput);
            grpOutput.Location = new Point(18, 261);
            grpOutput.Margin = new Padding(4, 5, 4, 5);
            grpOutput.Name = "grpOutput";
            grpOutput.Padding = new Padding(4, 5, 4, 5);
            grpOutput.Size = new Size(928, 278);
            grpOutput.TabIndex = 11;
            grpOutput.TabStop = false;
            grpOutput.Text = "執行日誌";
            // 
            // txtOutput
            // 
            txtOutput.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            txtOutput.BackColor = Color.FromArgb(40, 40, 40);
            txtOutput.BorderStyle = BorderStyle.None;
            txtOutput.Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtOutput.ForeColor = Color.Gainsboro;
            txtOutput.Location = new Point(4, 25);
            txtOutput.Margin = new Padding(4, 5, 4, 5);
            txtOutput.Name = "txtOutput";
            txtOutput.ReadOnly = true;
            txtOutput.Size = new Size(920, 243);
            txtOutput.TabIndex = 12;
            txtOutput.Text = "";
            // 
            // lblStatus
            // 
            lblStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblStatus.AutoSize = true;
            lblStatus.Font = new Font("Microsoft JhengHei UI", 12F, FontStyle.Bold, GraphicsUnit.Point, 136);
            lblStatus.Location = new Point(13, 543);
            lblStatus.Margin = new Padding(4, 0, 4, 0);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(147, 25);
            lblStatus.TabIndex = 0;
            lblStatus.Text = "狀態：偵測中...";
            lblStatus.DoubleClick += lblStatus_DoubleClick;
            // 
            // grpPhysPanel
            // 
            grpPhysPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            grpPhysPanel.Controls.Add(radPhysPanelDrv);
            grpPhysPanel.Controls.Add(radPhysPanelCS);
            grpPhysPanel.Location = new Point(18, 3);
            grpPhysPanel.Margin = new Padding(4, 5, 4, 5);
            grpPhysPanel.Name = "grpPhysPanel";
            grpPhysPanel.Padding = new Padding(4, 5, 4, 5);
            grpPhysPanel.Size = new Size(928, 82);
            grpPhysPanel.TabIndex = 2;
            grpPhysPanel.TabStop = false;
            grpPhysPanel.Text = "螢幕尺寸覆寫方式 (適用於非掌機)";
            // 
            // radPhysPanelDrv
            // 
            radPhysPanelDrv.AutoSize = true;
            radPhysPanelDrv.Location = new Point(28, 53);
            radPhysPanelDrv.Margin = new Padding(4, 5, 4, 5);
            radPhysPanelDrv.Name = "radPhysPanelDrv";
            radPhysPanelDrv.Size = new Size(560, 23);
            radPhysPanelDrv.TabIndex = 4;
            radPhysPanelDrv.Text = "PhysPanelDrv (驅動程式模式，進階替代方案，需停用安全啟動並啟用測試簽章)";
            radPhysPanelDrv.UseVisualStyleBackColor = true;
            // 
            // radPhysPanelCS
            // 
            radPhysPanelCS.AutoSize = true;
            radPhysPanelCS.Checked = true;
            radPhysPanelCS.Location = new Point(28, 25);
            radPhysPanelCS.Margin = new Padding(4, 5, 4, 5);
            radPhysPanelCS.Name = "radPhysPanelCS";
            radPhysPanelCS.Size = new Size(434, 23);
            radPhysPanelCS.TabIndex = 3;
            radPhysPanelCS.TabStop = true;
            radPhysPanelCS.Text = "PhysPanelCS (工作排程模式，預設建議方案，無需額外設定)";
            radPhysPanelCS.UseVisualStyleBackColor = true;
            // 
            // cboLanguage
            // 
            cboLanguage.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            cboLanguage.DropDownStyle = ComboBoxStyle.DropDownList;
            cboLanguage.Font = new Font("Microsoft JhengHei UI", 11F, FontStyle.Regular, GraphicsUnit.Point, 136);
            cboLanguage.FormattingEnabled = true;
            cboLanguage.Location = new Point(766, 542);
            cboLanguage.Margin = new Padding(4, 5, 4, 5);
            cboLanguage.Name = "cboLanguage";
            cboLanguage.Size = new Size(180, 31);
            cboLanguage.TabIndex = 1;
            cboLanguage.SelectedIndexChanged += cboLanguage_SelectedIndexChanged;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(9F, 19F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(964, 577);
            Controls.Add(cboLanguage);
            Controls.Add(grpPhysPanel);
            Controls.Add(lblStatus);
            Controls.Add(grpOutput);
            Controls.Add(grpActions);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Margin = new Padding(4, 5, 4, 5);
            MaximizeBox = false;
            MinimumSize = new Size(982, 624);
            Name = "MainForm";
            ShowIcon = false;
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Xbox 全螢幕體驗工具";
            Load += MainForm_Load;
            Shown += MainForm_Shown;
            grpActions.ResumeLayout(false);
            grpActions.PerformLayout();
            grpOutput.ResumeLayout(false);
            grpPhysPanel.ResumeLayout(false);
            grpPhysPanel.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        private System.Windows.Forms.Button btnEnable;
        private System.Windows.Forms.Button btnDisable;
        private System.Windows.Forms.GroupBox grpActions;
        private System.Windows.Forms.GroupBox grpOutput;
        private System.Windows.Forms.RichTextBox txtOutput;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.GroupBox grpPhysPanel;
        private System.Windows.Forms.RadioButton radPhysPanelDrv;
        private System.Windows.Forms.RadioButton radPhysPanelCS;
        private System.Windows.Forms.CheckBox chkStartKeyboardOnLogon;
        private System.Windows.Forms.ToolTip toolTip;
        private System.Windows.Forms.ComboBox cboLanguage;
        private System.Windows.Forms.Button btnOpenSettings;
        private System.Windows.Forms.Button btnCheckUpdates;
    }
}