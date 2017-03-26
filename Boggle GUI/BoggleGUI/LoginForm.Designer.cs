namespace BoggleGUI
{
    partial class LoginForm
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
            this.usernameBox = new System.Windows.Forms.TextBox();
            this.loginUsernameLabel = new System.Windows.Forms.Label();
            this.loginDurationBox = new System.Windows.Forms.TextBox();
            this.loginDomainBox = new System.Windows.Forms.TextBox();
            this.loginDomainLabel = new System.Windows.Forms.Label();
            this.loginDurationLabel = new System.Windows.Forms.Label();
            this.loginSubmitButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.loginLabel = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // usernameBox
            // 
            this.usernameBox.Location = new System.Drawing.Point(188, 37);
            this.usernameBox.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.usernameBox.Name = "usernameBox";
            this.usernameBox.Size = new System.Drawing.Size(354, 26);
            this.usernameBox.TabIndex = 0;
            // 
            // loginUsernameLabel
            // 
            this.loginUsernameLabel.AutoSize = true;
            this.loginUsernameLabel.Location = new System.Drawing.Point(94, 42);
            this.loginUsernameLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.loginUsernameLabel.Name = "loginUsernameLabel";
            this.loginUsernameLabel.Size = new System.Drawing.Size(51, 20);
            this.loginUsernameLabel.TabIndex = 3;
            this.loginUsernameLabel.Text = "label1";
            // 
            // loginDurationBox
            // 
            this.loginDurationBox.Location = new System.Drawing.Point(188, 117);
            this.loginDurationBox.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.loginDurationBox.Name = "loginDurationBox";
            this.loginDurationBox.Size = new System.Drawing.Size(148, 26);
            this.loginDurationBox.TabIndex = 4;
            // 
            // loginDomainBox
            // 
            this.loginDomainBox.Location = new System.Drawing.Point(188, 77);
            this.loginDomainBox.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.loginDomainBox.Name = "loginDomainBox";
            this.loginDomainBox.Size = new System.Drawing.Size(354, 26);
            this.loginDomainBox.TabIndex = 5;
            // 
            // loginDomainLabel
            // 
            this.loginDomainLabel.AutoSize = true;
            this.loginDomainLabel.Location = new System.Drawing.Point(68, 82);
            this.loginDomainLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.loginDomainLabel.Name = "loginDomainLabel";
            this.loginDomainLabel.Size = new System.Drawing.Size(51, 20);
            this.loginDomainLabel.TabIndex = 6;
            this.loginDomainLabel.Text = "label2";
            // 
            // loginDurationLabel
            // 
            this.loginDurationLabel.AutoSize = true;
            this.loginDurationLabel.Location = new System.Drawing.Point(64, 122);
            this.loginDurationLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.loginDurationLabel.Name = "loginDurationLabel";
            this.loginDurationLabel.Size = new System.Drawing.Size(51, 20);
            this.loginDurationLabel.TabIndex = 7;
            this.loginDurationLabel.Text = "label3";
            // 
            // loginSubmitButton
            // 
            this.loginSubmitButton.Location = new System.Drawing.Point(225, 157);
            this.loginSubmitButton.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.loginSubmitButton.Name = "loginSubmitButton";
            this.loginSubmitButton.Size = new System.Drawing.Size(112, 35);
            this.loginSubmitButton.TabIndex = 8;
            this.loginSubmitButton.Text = "button1";
            this.loginSubmitButton.UseVisualStyleBackColor = true;
            this.loginSubmitButton.Click += new System.EventHandler(this.SubmitLoginButton_Click);
            // 
            // cancelButton
            // 
            this.cancelButton.Location = new System.Drawing.Point(346, 157);
            this.cancelButton.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(112, 35);
            this.cancelButton.TabIndex = 9;
            this.cancelButton.Text = "button1";
            this.cancelButton.UseVisualStyleBackColor = true;
            this.cancelButton.Click += new System.EventHandler(this.CancelLogin_Click);
            // 
            // loginLabel
            // 
            this.loginLabel.AutoSize = true;
            this.loginLabel.Location = new System.Drawing.Point(184, 205);
            this.loginLabel.Name = "loginLabel";
            this.loginLabel.Size = new System.Drawing.Size(51, 20);
            this.loginLabel.TabIndex = 10;
            this.loginLabel.Text = "label1";
            // 
            // Login
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(580, 234);
            this.Controls.Add(this.loginLabel);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.loginSubmitButton);
            this.Controls.Add(this.loginDurationLabel);
            this.Controls.Add(this.loginDomainLabel);
            this.Controls.Add(this.loginDomainBox);
            this.Controls.Add(this.loginDurationBox);
            this.Controls.Add(this.loginUsernameLabel);
            this.Controls.Add(this.usernameBox);
            this.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.Name = "Login";
            this.Text = "Form1";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox usernameBox;
        private System.Windows.Forms.Label loginUsernameLabel;
        private System.Windows.Forms.TextBox loginDurationBox;
        private System.Windows.Forms.TextBox loginDomainBox;
        private System.Windows.Forms.Label loginDomainLabel;
        private System.Windows.Forms.Label loginDurationLabel;
        private System.Windows.Forms.Button loginSubmitButton;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.Label loginLabel;
    }
}