// Xbox Fullscreen Experience Tool
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

namespace XboxFullscreenExperienceTool
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
            this.btnEnable = new System.Windows.Forms.Button();
            this.btnDisable = new System.Windows.Forms.Button();
            this.grpActions = new System.Windows.Forms.GroupBox();
            this.grpOutput = new System.Windows.Forms.GroupBox();
            this.txtOutput = new System.Windows.Forms.RichTextBox();
            this.lblStatus = new System.Windows.Forms.Label();
            this.cboLanguage = new System.Windows.Forms.ComboBox();
            this.grpPhysPanel = new System.Windows.Forms.GroupBox();
            this.radPhysPanelDrv = new System.Windows.Forms.RadioButton();
            this.radPhysPanelCS = new System.Windows.Forms.RadioButton();
            this.grpActions.SuspendLayout();
            this.grpOutput.SuspendLayout();
            this.grpPhysPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnEnable
            // 
            this.btnEnable.Font = new System.Drawing.Font("Microsoft JhengHei UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.btnEnable.ForeColor = System.Drawing.Color.DarkGreen;
            this.btnEnable.Location = new System.Drawing.Point(19, 29);
            this.btnEnable.Name = "btnEnable";
            this.btnEnable.Size = new System.Drawing.Size(260, 50);
            this.btnEnable.TabIndex = 3;
            this.btnEnable.Text = "啟用 Xbox 全螢幕體驗";
            this.btnEnable.UseVisualStyleBackColor = true;
            this.btnEnable.Click += new System.EventHandler(this.btnEnable_Click);
            // 
            // btnDisable
            // 
            this.btnDisable.Font = new System.Drawing.Font("Microsoft JhengHei UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.btnDisable.ForeColor = System.Drawing.Color.DarkRed;
            this.btnDisable.Location = new System.Drawing.Point(295, 29);
            this.btnDisable.Name = "btnDisable";
            this.btnDisable.Size = new System.Drawing.Size(260, 50);
            this.btnDisable.TabIndex = 4;
            this.btnDisable.Text = "停用並還原";
            this.btnDisable.UseVisualStyleBackColor = true;
            this.btnDisable.Click += new System.EventHandler(this.btnDisable_Click);
            // 
            // grpActions
            // 
            this.grpActions.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpActions.Controls.Add(this.btnDisable);
            this.grpActions.Controls.Add(this.btnEnable);
            this.grpActions.Location = new System.Drawing.Point(12, 122);
            this.grpActions.Name = "grpActions";
            this.grpActions.Size = new System.Drawing.Size(574, 95);
            this.grpActions.TabIndex = 5;
            this.grpActions.TabStop = false;
            this.grpActions.Text = "操作";
            // 
            // grpOutput
            // 
            this.grpOutput.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpOutput.Controls.Add(this.txtOutput);
            this.grpOutput.Location = new System.Drawing.Point(12, 223);
            this.grpOutput.Name = "grpOutput";
            this.grpOutput.Size = new System.Drawing.Size(574, 154);
            this.grpOutput.TabIndex = 6;
            this.grpOutput.TabStop = false;
            this.grpOutput.Text = "執行日誌";
            // 
            // txtOutput
            // 
            this.txtOutput.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(40)))), ((int)(((byte)(40)))));
            this.txtOutput.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtOutput.Font = new System.Drawing.Font("Consolas", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtOutput.ForeColor = System.Drawing.Color.Gainsboro;
            this.txtOutput.Location = new System.Drawing.Point(3, 18);
            this.txtOutput.Name = "txtOutput";
            this.txtOutput.ReadOnly = true;
            this.txtOutput.Size = new System.Drawing.Size(568, 133);
            this.txtOutput.TabIndex = 0;
            this.txtOutput.Text = "";
            // 
            // lblStatus
            // 
            this.lblStatus.AutoSize = true;
            this.lblStatus.Font = new System.Drawing.Font("Microsoft JhengHei UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.lblStatus.Location = new System.Drawing.Point(12, 8);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(105, 20);
            this.lblStatus.TabIndex = 7;
            this.lblStatus.Text = "狀態：偵測中...";
            // 
            // cboLanguage
            // 
            this.cboLanguage.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cboLanguage.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboLanguage.Font = new System.Drawing.Font("Microsoft JhengHei UI", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.cboLanguage.FormattingEnabled = true;
            this.cboLanguage.Location = new System.Drawing.Point(466, 8);
            this.cboLanguage.Name = "cboLanguage";
            this.cboLanguage.Size = new System.Drawing.Size(121, 27);
            this.cboLanguage.TabIndex = 8;
            this.cboLanguage.SelectedIndexChanged += new System.EventHandler(this.cboLanguage_SelectedIndexChanged);
            // 
            // grpPhysPanel
            // 
            this.grpPhysPanel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpPhysPanel.Controls.Add(this.radPhysPanelDrv);
            this.grpPhysPanel.Controls.Add(this.radPhysPanelCS);
            this.grpPhysPanel.Location = new System.Drawing.Point(12, 41);
            this.grpPhysPanel.Name = "grpPhysPanel";
            this.grpPhysPanel.Size = new System.Drawing.Size(574, 75);
            this.grpPhysPanel.TabIndex = 9;
            this.grpPhysPanel.TabStop = false;
            this.grpPhysPanel.Text = "螢幕尺寸覆寫方式 (適用於非掌機)";
            // 
            // radPhysPanelDrv
            // 
            this.radPhysPanelDrv.AutoSize = true;
            this.radPhysPanelDrv.Location = new System.Drawing.Point(19, 48);
            this.radPhysPanelDrv.Name = "radPhysPanelDrv";
            this.radPhysPanelDrv.Size = new System.Drawing.Size(378, 16);
            this.radPhysPanelDrv.TabIndex = 1;
            this.radPhysPanelDrv.Text = "PhysPanelDrv (驅動程式模式，穩定性高，需啟用測試簽章";
            this.radPhysPanelDrv.UseVisualStyleBackColor = true;
            // 
            // radPhysPanelCS
            // 
            this.radPhysPanelCS.AutoSize = true;
            this.radPhysPanelCS.Checked = true;
            this.radPhysPanelCS.Location = new System.Drawing.Point(19, 22);
            this.radPhysPanelCS.Name = "radPhysPanelCS";
            this.radPhysPanelCS.Size = new System.Drawing.Size(326, 16);
            this.radPhysPanelCS.TabIndex = 0;
            this.radPhysPanelCS.TabStop = true;
            this.radPhysPanelCS.Text = "PhysPanelCS (工作排程模式，安全性高，預設選項)";
            this.radPhysPanelCS.UseVisualStyleBackColor = true;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(598, 389);
            this.Controls.Add(this.grpPhysPanel);
            this.Controls.Add(this.cboLanguage);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.grpOutput);
            this.Controls.Add(this.grpActions);
            this.MinimumSize = new System.Drawing.Size(614, 428);
            this.Name = "MainForm";
            this.ShowIcon = false;
            this.Text = "Xbox 全螢幕體驗工具";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.grpActions.ResumeLayout(false);
            this.grpOutput.ResumeLayout(false);
            this.grpPhysPanel.ResumeLayout(false);
            this.grpPhysPanel.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.Button btnEnable;
        private System.Windows.Forms.Button btnDisable;
        private System.Windows.Forms.GroupBox grpActions;
        private System.Windows.Forms.GroupBox grpOutput;
        private System.Windows.Forms.RichTextBox txtOutput;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.ComboBox cboLanguage;
        private System.Windows.Forms.GroupBox grpPhysPanel;
        private System.Windows.Forms.RadioButton radPhysPanelDrv;
        private System.Windows.Forms.RadioButton radPhysPanelCS;
    }
}