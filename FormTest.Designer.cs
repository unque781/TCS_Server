namespace TCS.ServerV2
{
    partial class FormTest
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.bVehicleRemove = new System.Windows.Forms.Button();
            this.bVehicleInstall = new System.Windows.Forms.Button();
            this.bPMSet = new System.Windows.Forms.Button();
            this.bPMClear = new System.Windows.Forms.Button();
            this.bBMSet = new System.Windows.Forms.Button();
            this.bBMClear = new System.Windows.Forms.Button();
            this.cbUnit = new System.Windows.Forms.ComboBox();
            this.btnCimCarrierSend = new System.Windows.Forms.Button();
            this.tbCarrerId = new System.Windows.Forms.TextBox();
            this.cbMachine = new System.Windows.Forms.ComboBox();
            this.cbVehicle = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // bVehicleRemove
            // 
            this.bVehicleRemove.Location = new System.Drawing.Point(71, 153);
            this.bVehicleRemove.Name = "bVehicleRemove";
            this.bVehicleRemove.Size = new System.Drawing.Size(100, 44);
            this.bVehicleRemove.TabIndex = 0;
            this.bVehicleRemove.Text = "Vehicle Remove";
            this.bVehicleRemove.UseVisualStyleBackColor = true;
            this.bVehicleRemove.Click += new System.EventHandler(this.bVehicleRemove_Click);
            // 
            // bVehicleInstall
            // 
            this.bVehicleInstall.Location = new System.Drawing.Point(71, 103);
            this.bVehicleInstall.Name = "bVehicleInstall";
            this.bVehicleInstall.Size = new System.Drawing.Size(100, 44);
            this.bVehicleInstall.TabIndex = 1;
            this.bVehicleInstall.Text = "Vehicle Install";
            this.bVehicleInstall.UseVisualStyleBackColor = true;
            this.bVehicleInstall.Click += new System.EventHandler(this.bVehicleInstall_Click);
            // 
            // bPMSet
            // 
            this.bPMSet.Location = new System.Drawing.Point(218, 103);
            this.bPMSet.Name = "bPMSet";
            this.bPMSet.Size = new System.Drawing.Size(100, 44);
            this.bPMSet.TabIndex = 3;
            this.bPMSet.Text = "PM";
            this.bPMSet.UseVisualStyleBackColor = true;
            this.bPMSet.Click += new System.EventHandler(this.bPMSet_Click);
            // 
            // bPMClear
            // 
            this.bPMClear.Location = new System.Drawing.Point(218, 153);
            this.bPMClear.Name = "bPMClear";
            this.bPMClear.Size = new System.Drawing.Size(100, 44);
            this.bPMClear.TabIndex = 2;
            this.bPMClear.Text = "PM Clear";
            this.bPMClear.UseVisualStyleBackColor = true;
            this.bPMClear.Click += new System.EventHandler(this.bPMClear_Click);
            // 
            // bBMSet
            // 
            this.bBMSet.Location = new System.Drawing.Point(481, 103);
            this.bBMSet.Name = "bBMSet";
            this.bBMSet.Size = new System.Drawing.Size(100, 44);
            this.bBMSet.TabIndex = 5;
            this.bBMSet.Text = "BM";
            this.bBMSet.UseVisualStyleBackColor = true;
            this.bBMSet.Click += new System.EventHandler(this.bBMSet_Click);
            // 
            // bBMClear
            // 
            this.bBMClear.Location = new System.Drawing.Point(481, 153);
            this.bBMClear.Name = "bBMClear";
            this.bBMClear.Size = new System.Drawing.Size(100, 44);
            this.bBMClear.TabIndex = 4;
            this.bBMClear.Text = "BM Clear";
            this.bBMClear.UseVisualStyleBackColor = true;
            this.bBMClear.Click += new System.EventHandler(this.bBMClear_Click);
            // 
            // cbUnit
            // 
            this.cbUnit.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbUnit.FormattingEnabled = true;
            this.cbUnit.Location = new System.Drawing.Point(227, 37);
            this.cbUnit.Name = "cbUnit";
            this.cbUnit.Size = new System.Drawing.Size(150, 20);
            this.cbUnit.TabIndex = 6;
            // 
            // btnCimCarrierSend
            // 
            this.btnCimCarrierSend.Location = new System.Drawing.Point(324, 103);
            this.btnCimCarrierSend.Name = "btnCimCarrierSend";
            this.btnCimCarrierSend.Size = new System.Drawing.Size(140, 26);
            this.btnCimCarrierSend.TabIndex = 3;
            this.btnCimCarrierSend.Text = "CARRIER SEND";
            this.btnCimCarrierSend.UseVisualStyleBackColor = true;
            this.btnCimCarrierSend.Click += new System.EventHandler(this.btnCimCarrierSend1_Click);
            // 
            // tbCarrerId
            // 
            this.tbCarrerId.Location = new System.Drawing.Point(324, 131);
            this.tbCarrerId.Name = "tbCarrerId";
            this.tbCarrerId.Size = new System.Drawing.Size(140, 21);
            this.tbCarrerId.TabIndex = 7;
            // 
            // cbMachine
            // 
            this.cbMachine.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbMachine.FormattingEnabled = true;
            this.cbMachine.Location = new System.Drawing.Point(71, 37);
            this.cbMachine.Name = "cbMachine";
            this.cbMachine.Size = new System.Drawing.Size(150, 20);
            this.cbMachine.TabIndex = 6;
            // 
            // cbVehicle
            // 
            this.cbVehicle.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbVehicle.FormattingEnabled = true;
            this.cbVehicle.Location = new System.Drawing.Point(383, 37);
            this.cbVehicle.Name = "cbVehicle";
            this.cbVehicle.Size = new System.Drawing.Size(150, 20);
            this.cbVehicle.TabIndex = 6;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(71, 20);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(61, 12);
            this.label1.TabIndex = 8;
            this.label1.Text = "MACHINE";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(225, 20);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(33, 12);
            this.label2.TabIndex = 8;
            this.label2.Text = "UNIT";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(381, 20);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(56, 12);
            this.label3.TabIndex = 8;
            this.label3.Text = "VEHICLE";
            // 
            // FormTest
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(608, 290);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.tbCarrerId);
            this.Controls.Add(this.cbMachine);
            this.Controls.Add(this.cbVehicle);
            this.Controls.Add(this.cbUnit);
            this.Controls.Add(this.bBMSet);
            this.Controls.Add(this.bBMClear);
            this.Controls.Add(this.btnCimCarrierSend);
            this.Controls.Add(this.bPMSet);
            this.Controls.Add(this.bPMClear);
            this.Controls.Add(this.bVehicleInstall);
            this.Controls.Add(this.bVehicleRemove);
            this.Name = "FormTest";
            this.Text = "FormTest";
            this.Load += new System.EventHandler(this.FormTest_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button bVehicleRemove;
        private System.Windows.Forms.Button bVehicleInstall;
        private System.Windows.Forms.Button bPMSet;
        private System.Windows.Forms.Button bPMClear;
        private System.Windows.Forms.Button bBMSet;
        private System.Windows.Forms.Button bBMClear;
        private System.Windows.Forms.ComboBox cbUnit;
        private System.Windows.Forms.Button btnCimCarrierSend;
        private System.Windows.Forms.TextBox tbCarrerId;
        private System.Windows.Forms.ComboBox cbMachine;
        private System.Windows.Forms.ComboBox cbVehicle;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
    }
}