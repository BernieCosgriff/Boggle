using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Windows.Forms;

namespace BoggleGUI
{
    public partial class BoardForm : Form
    {
        /// <summary>
        /// Called when the user requests to create a new game.
        /// </summary>
        public event Action RequestNewGameEvent;

        /// <summary>
        /// Called when the user plays a word in a boggle game.
        /// </summary>
        public event Action<string> PlayWordEvent;

        /// <summary>
        /// Called when the user cancels a word that has been played
        /// but not yet responded to by the server.
        /// </summary>
        public event Action CancelPlayWordEvent;

        // The datetime used to format/show time remaining.
        private DateTime dt = new DateTime();

        // The time remaining in the game, in seconds.
        private int timeSec;

        public BoardForm()
        {
            InitializeComponent();
            StartPosition = FormStartPosition.CenterScreen;
            ShowBoardLabels(false);
            //Initialize all of the labels' and buttons' text
            this.Text = "Boggle";
            wordLabel.Text = "Word:";
            enterButton.Text = "Enter";
            cancelButton.Text = "Cancel";
            newGameButton.Text = "Cancel Game";
            userNameLabel.Text = "";
            opponentTotalLabel.Text = "Total Score:";
            opponentNameLabel.Text = "";
            playerTotalLabel.Text = "Total Score:";
            playerNameLabel.Text = "";
            statuslabel.Text = "Time Remaining:";
            //Disables the cancel button until we start a game
            cancelButton.Enabled = false;
        }

        /// <summary>
        /// Displays the time remaining in the game.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TimeTick(object sender, EventArgs e)
        {
            //Update the progress bar and timer
            try
            {
                Invoke((Action) (() =>
                {
                    progressBar.PerformStep();
                    if (timeSec != 0)
                    {
                        timeSec--;
                    }
                    statuslabel.Text = "Time Remaining: " + dt.AddSeconds(timeSec).ToString("mm:ss");
                }));
            } catch (Exception) { }
        }

        /// <summary>
        /// Sets the time remaining int the game
        /// </summary>
        /// <param name="timeRemaining"></param>
        public void SetTimeRemaining(int timeRemaining)
        {
            //Set up timer
            timeSec = timeRemaining;
            dt.AddSeconds(timeSec);
            timer.Tick += TimeTick;
            timer.Interval = 1000;
            //Set up progess bar
            progressBar.Maximum = timeRemaining;
            progressBar.Minimum = 1;
            progressBar.Value = 1;
            progressBar.Step = 1;
        }

        /// <summary>
        /// Sets the player's name
        /// </summary>
        /// <param name="name"></param>
        public void SetPlayerName(string name)
        {
            try
            {
                Invoke((Action)(() => {
                    userNameLabel.Text = playerNameLabel.Text = "Username: " + name;
                    playerNameLabel.Text = name;
                }));
            } catch (Exception) { }
        }

        /// <summary>
        /// Sets the opponent's name
        /// </summary>
        /// <param name="name"></param>
        public void SetOpponentname(string name)
        {
            try
            {
                Invoke((Action)(() =>
                {
                    opponentNameLabel.Text = name;
                }));
            } catch (Exception) { }
        }

        /// <summary>
        /// Set's the opponent's score
        /// </summary>
        /// <param name="score"></param>
        public void SetOpponentScore(int score)
        {
            try
            {
                Invoke((Action)(() =>
                {
                    opponentTotalLabel.Text = score + "";
                }));
            } catch (Exception) { }
        }

        /// <summary>
        /// Sets the player's score
        /// </summary>
        /// <param name="score"></param>
        public void SetPlayerScore(int score)
        {
            try
            {
                Invoke((Action)(() =>
                {
                    playerTotalLabel.Text = score + "";
                }));
            } catch (Exception) { }
          
        }

        /// <summary>
        /// Adds the played word to the ListBox
        /// </summary>
        public void SetPlayedWord(string word, int score)//Changed
        {
            TextInfo text = new CultureInfo("en-US", false).TextInfo;
            wordListBox.Items.Add(text.ToTitleCase(word.ToLower() + " (" + score + ")"));
        }

        /// <summary>
        /// Shows the given board.
        /// </summary>
        public void SetBoard(string board)
        {
            //Make the string a char array
            char[] letters = board.ToCharArray();
            Control control = new Control();
            for (int i = 0; i < 16; i++)
            {
                control = boggleGrid.GetControlFromPosition(i / 4, i % 4);
                if (letters[i] == 'Q')
                {
                    control.Text = "Qu";
                }
                else
                {
                    control.Text = letters[i] + "";
                }
            }
            ShowBoardLabels(true);
        }

        /// <summary>
        /// If b is true true, shows the board labels. 
        /// </summary>
        private void ShowBoardLabels(bool b)
        {
            if(b == false)
            {
                label1.Hide();
                label2.Hide();
                label3.Hide();
                label4.Hide();
                label5.Hide();
                label6.Hide();
                label7.Hide();
                label8.Hide();
                label9.Hide();
                label10.Hide();
                label11.Hide();
                label12.Hide();
                label13.Hide();
                label14.Hide();
                label15.Hide();
                label16.Hide();
            }
            if (b == true)
            {
                label1.Show();
                label2.Show();
                label3.Show();
                label4.Show();
                label5.Show();
                label6.Show();
                label7.Show();
                label8.Show();
                label9.Show();
                label10.Show();
                label11.Show();
                label12.Show();
                label13.Show();
                label14.Show();
                label15.Show();
                label16.Show();
            }
        }

        /// <summary>
        /// Tells the user that the game is pending. 
        /// </summary>
        public void ShowGamePending()
        {
            statuslabel.Text = "Searching for Opponent...";
            enterButton.Enabled = false;
            wordBox.Enabled = false;
        }

        /// <summary>
        /// Enables the enter/cancel buttons and wordbox. Shows the time remaining in the game.
        /// </summary>
        public void ShowGameOngoing()
        {
            timer.Start();
            enterButton.Enabled = true;
            cancelButton.Enabled = true;
            wordBox.Enabled = true;
        }

        /// <summary>
        /// Shows the words played by the opposing player, the words played by the user, as well as both parties' 
        /// scores.
        /// </summary>
        public void ShowGameOverSummary(List<KeyValuePair<string, int>> wordsPlayedByOpponent, int opponentScore)
        {
            Invoke((Action)(() =>
            {
                TextInfo text = new CultureInfo("en-US", false).TextInfo;
                //Stop the timer
                timer.Stop();
                //Disable buttons
                enterButton.Enabled = false;
                cancelButton.Enabled = false;
                //Change the status
                statuslabel.Text = "Game Over";
                //Change the button text
                newGameButton.Text = "New Game";
                //Disable the word textbox
                wordBox.Enabled = false;
                //Show the words played by opponent
                if (wordsPlayedByOpponent.Count == 0)
                {
                    postGameList.Items.Add("No Words Played");
                }
                else
                {
                    foreach (KeyValuePair<string, int> pair in wordsPlayedByOpponent)
                    {
                        Debug.WriteLine(pair.Key);
                        postGameList.Items.Add(text.ToTitleCase(pair.Key.ToString().ToLower() + " (" + pair.Value.ToString() + ")"));
                    }
                }
            }));
        }

        /// <summary>
        /// Show the user that an error occurred on the server side.
        /// </summary>
        public void ShowServerError()
        {
            MessageBox.Show("We're sorry. An error occurred on the server processing your request.");
        }

        /// <summary>
        /// Fires cancel event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cancelExitButton_Click(object sender, EventArgs e)
        {
            if (CancelPlayWordEvent != null)
            {
                CancelPlayWordEvent();
            }
        }

        /// <summary>
        /// Invokes the PlayWordEvent with the current contents of the wordBox. 
        /// </summary>
        private void playWordButton_Click(object sender, EventArgs e)
        {
            if (PlayWordEvent != null)
            {
                PlayWordEvent(wordBox.Text);
                wordBox.Text = "";
            }
        }

        /// <summary>
        /// Invokes the RequestNewGameEvent.
        /// </summary>
        private void newGameButton_Click(object sender, EventArgs e)
        {
            if (RequestNewGameEvent != null)
            {
                RequestNewGameEvent();
            }
        }

        /// <summary>
        /// Shows information about the app creators. 
        /// </summary>
        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Created by Bernard Cosgriff and Alex Steele on 3/24/2016");
        }

        /// <summary>
        /// Shows a menu describing the UI mechanics.
        /// </summary>
        private void howToUseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("To play a word, enter your word into the text box and click the enter button.\n\n"
                + "The Time remaining is displayed above the progress bar located above the board.\n\n"
                + "You may cancel a play word request by selecting the cancel button.\n\nThe box to the right of the board"
                + " displays your words guessed and their corresponding scores.\n\nThe box to the left of "
                + "the board will display your opponent's words and score after the game is finished.\n\n"
                + "To start a new game select the 'New Game' button.\n\nTo exit the current game and go back"
                + " to the login screen, press the 'Exit Game' button.");
        }
    }
}
