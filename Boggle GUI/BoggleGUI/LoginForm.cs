using System;
using System.Windows.Forms;

namespace BoggleGUI
{
    public partial class LoginForm : Form
    {
        /// <summary>
        /// Invocation indicates that user has attempted to login with the 
        /// passed username, server domain name, and desired game duration. 
        /// </summary>
        public event Action<string, string, int> SubmitLoginEvent;

        /// <summary>
        /// Invocation indicates that the user would like to cancel any pending
        /// login. 
        /// </summary>
        public event Action CancelLoginEvent;

        public LoginForm()
        {
            InitializeComponent();
            StartPosition = FormStartPosition.CenterScreen;
            //Set all of the text
            loginLabel.Hide();
            loginUsernameLabel.Text = "Username:";
            loginDomainLabel.Text = "Domain Name:";
            loginDurationLabel.Text = "Game Duration:";
            loginSubmitButton.Text = "Find Game";
            cancelButton.Text = "Cancel";
            this.Text = "Login Screen";
            loginDomainBox.Text = "http://bogglecs3500s16.azurewebsites.net";

            cancelButton.Enabled = false;
        }

        /// <summary>
        /// Shows the user that a login is pending.
        /// </summary>
        public void ShowPendingLogin()
        {
            Invoke((Action)(() => {
                loginSubmitButton.Enabled = false;
                loginLabel.Text = "Verifying Credentials...";
                loginLabel.Show();
            }));
        }

        /// <summary>
        /// Notifies the user that a login attempt was canceled and allows
        /// him to request a new login.
        /// </summary>
        public void ShowCanceledLogin()
        {
            Invoke((Action)(() => {
                loginLabel.Text = "";
                loginSubmitButton.Enabled = true;
                cancelButton.Enabled = false;

                MessageBox.Show("Canceled login attempt.");
            }));
        }

        /// <summary>
        /// Shows the user that a login failed. 
        /// </summary>
        public void ShowFailedLogin()
        {
            loginSubmitButton.Enabled = true;
            MessageBox.Show("Unable to create game. Please ensure that server domain name " +
                "is valid and game duration is between 5 and 120 seconds.");
        }

        /// <summary>
        /// Fires CancelEvent
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CancelLogin_Click(object sender, EventArgs e)
        {
            CancelLoginEvent?.Invoke();
        }

        /// <summary>
        /// If all fields are valid, fires the SubmitLoginEvent
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SubmitLoginButton_Click(object sender, EventArgs e)
        {
            if (SubmitLoginEvent != null)
            {
                int result;
                if (string.IsNullOrEmpty(loginDomainBox.Text))
                {
                    MessageBox.Show("Please Enter a Domain Name.");
                }
                else if (string.IsNullOrEmpty(usernameBox.Text))
                {
                    MessageBox.Show("Please Enter a Username.");
                }
                else if (Int32.TryParse(loginDurationBox.Text, out result))
                {
                    cancelButton.Enabled = true;
                    SubmitLoginEvent(usernameBox.Text, loginDomainBox.Text, result);
                }
                else
                {
                    MessageBox.Show("Please Enter a Valid Game Duration.");
                }
            }
        }
    }
}
