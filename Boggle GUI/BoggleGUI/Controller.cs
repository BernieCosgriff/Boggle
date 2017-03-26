using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using System.Dynamic;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;

namespace BoggleGUI
{
    /// <summary>
    /// Controls a boggle game session.
    /// </summary>
    public class Controller
    {
        // Fired when the user attemps to exit the application.
        public event Action WindowsClosedEvent;

        // Views.
        private LoginForm loginView;
        private BoardForm boardView;

        // Client
        private HttpClient boggleClient;

        // Dictates when periodic updates to the boardview occur during an ongoing game.
        private System.Timers.Timer updateTimer;

        // User token received from the server.
        private string userToken;

        // Nickname of user.
        private string userNickname;

        // ID of the current game received from the server.
        private int? gameId;

        // The user's score in the current game.
        private int? userScore;
        
        public Controller()
        {
            loginView = new LoginForm();
            boardView = new BoardForm();

            boggleClient = null;

            updateTimer = null;
            userNickname = null;
            userToken = null;
            userScore = null;
            gameId = null;

            InitSubscriptions();
            loginView.Show();
        }

        // Initialize Subscriptions to view events.

        private void InitSubscriptions()
        {
            InitLoginSubscriptions();
            InitBoardSubscriptions();
        }

        private void InitLoginSubscriptions()
        {
            loginView.FormClosed += (sender, args) =>
            {
                WindowsClosedEvent?.Invoke();
            };

            loginView.SubmitLoginEvent += HandleInitialLogin;
            loginView.CancelLoginEvent += HandleCancelLogin;

        }

        private void InitBoardSubscriptions()
        {
            boardView.FormClosed += (sender, args) =>
            {
                NullifyGameState();
                WindowsClosedEvent?.Invoke();
            };

            boardView.RequestNewGameEvent += HandleRequestNewGame;
            boardView.CancelPlayWordEvent += HandleCancelPlayWord;
            boardView.PlayWordEvent += HandlePlayWord;
        }

        /// <summary>
        /// Shows the game UI.
        /// </summary>
        public void Run()
        {
            loginView.Show();
        }

        /// <summary>
        /// Attempts to register the user on the boggle server. If registration succeeds, requests a new 
        /// boggle game from the server. If the request succeeds, begins a new boggle game.
        /// </summary>
        private async void HandleInitialLogin(string nickname, string domain, int gameDuration)
        {
            // Vet the input.
            if (string.IsNullOrEmpty(nickname) ||
                string.IsNullOrEmpty(domain) ||
                !Uri.IsWellFormedUriString(domain, UriKind.Absolute) ||
                gameDuration < 5 ||
                gameDuration > 120)
            {
                loginView.ShowFailedLogin();
                return;
            }

            loginView.ShowPendingLogin();

            // Initialize our boggle client.
            boggleClient = new HttpClient();
            boggleClient.BaseAddress = new Uri(domain);

            // Asynchronously attempt to get a user token from the server.
            try
            {
                var userTokenRequestResult = await RequestUserToken(boggleClient, nickname);
                if (!userTokenRequestResult.Item1)
                {
                    loginView.ShowFailedLogin();
                    return;
                }
                // Nickname and token were appropriate. Save.
                userNickname = nickname;
                userToken = userTokenRequestResult.Item2;
            }
            catch (TaskCanceledException)
            {
                // User cancelled login.
                return;
            }

            // Hide loginView. Show board.
            loginView.Hide();
            boardView.Show();
            boardView.ShowGamePending();

            // Asynchronously attempt to create a new game.
            try
            {
                var gameRequestResult = await RequestNewGame(boggleClient, userToken, gameDuration);
                if (!gameRequestResult.Item1)
                {
                    loginView.ShowFailedLogin();
                    return;
                }
                // Initialize gamestate now that we have gameID.
                gameId = gameRequestResult.Item2;
                updateTimer = new System.Timers.Timer(1000);
                updateTimer.Elapsed += (a, e) => RequestGameStatusUpdate();
                userScore = 0;
                boardView.SetPlayerName(nickname);
            }
            catch (TaskCanceledException)
            {
                HandleRequestNewGame();
                return;
            }

            // Start the game.
            StartGame();
        }

        /// <summary>
        /// Requests a boggle user token from the given HttpClient. Returns a tuple whose first field
        /// indicates if the request was accpeted and whose second field is either null (if the request
        /// did not succeed) or a user token string (if the request was successful).
        /// </summary>
        private static async Task<Tuple<bool, string>> RequestUserToken(HttpClient client, string userNickName)
        {
            dynamic reqBody = new ExpandoObject();
            reqBody.Nickname = userNickName;
            StringContent reqJson = GetJsonForRequestBody(reqBody);
            
            var response = await client.PostAsync("/BoggleService.svc/users", reqJson);
            switch (response.StatusCode)
            {
                case HttpStatusCode.Forbidden:
                    return new Tuple<bool, string>(false, null);
                case HttpStatusCode.Created:
                    string userToken = GetDeserializedResponseContent(response).UserToken;
                    return new Tuple<bool, string>(true, userToken);
                default:
                    // The server name was invalid.
                    return new Tuple<bool, string>(false, null);
            }
        }

        /// <summary>
        /// Requests a new boggle game from the given HttpClient. Returns a tuple whose first field indicates whether the 
        /// request and whose second field is either null (if the request was denied) or an integer game ID (if the request was successful).
        /// </summary>
        private static async Task<Tuple<bool, int?>> RequestNewGame(HttpClient client, string userToken, int duration)
        {
            dynamic reqBody = new ExpandoObject();
            reqBody.UserToken = userToken;
            reqBody.TimeLimit = duration;
            StringContent reqJson = GetJsonForRequestBody(reqBody);

            var response = await client.PostAsync("/BoggleService.svc/games", reqJson);
            if (response.StatusCode == HttpStatusCode.Created ||
                response.StatusCode == HttpStatusCode.Accepted)
            {
                string gameIdStr = GetDeserializedResponseContent(response).GameID;
                int gameIdInt = int.Parse(gameIdStr);
                return new Tuple<bool, int?>(true, gameIdInt);
            }
            else
            {
                // The server name is not a boggle server or the game could not be created.
                return new Tuple<bool, int?>(false, null);

            }
        }

        /// <summary>
        /// Starts a new game of boggle by initializing the boardView's UI with starting gamestatus info.
        /// </summary>
        private async void StartGame()
        {
            Debug.Assert(IsGameStateValid(), GetGameStateString());
            try
            {
                var response = await ReqGameStateUntilActiveOrFailure();
                if (response == null ||
                    response.StatusCode == HttpStatusCode.Forbidden)
                {
                    // TODO: This may not be the smoothest way to handle a forbidden response.
                    boardView.ShowServerError();
                    HandleRequestNewGame();
                    return;
                }
                dynamic responseObject = GetDeserializedResponseContent(response);
                dynamic opposingPlayer = ((string) responseObject.Player1.Nickname).Equals(userNickname)
                    ? responseObject.Player2
                    : responseObject.Player1;
                string opponentName = opposingPlayer.Nickname;
                int opponentScore = int.Parse((string) opposingPlayer.Score);
                int playerScore = ((string) responseObject.Player1.Nickname).Equals(userNickname)
                    ? int.Parse((string) responseObject.Player1.Score)
                    : int.Parse((string) responseObject.Player2.Score);
                string board = responseObject.Board;
                int timeLeft = int.Parse((string) responseObject.TimeLeft);

                boardView.SetOpponentname(opponentName);
                boardView.SetOpponentScore(opponentScore);
                boardView.SetPlayerScore(playerScore);
                boardView.SetBoard(board);
                boardView.SetTimeRemaining(timeLeft);

                boardView.ShowGameOngoing();
                updateTimer.Start();
            }
            catch (NullReferenceException)
            {
                // Http request was cancelled during its execution.
                return;
            }
        }

        /// <summary>
        /// Makes gamestatus requests to the boggleClient until either the client returns a response indicating either that
        /// 1)the game ID was invalid (in which case returns the response), 2) the gamestatus is active (in which case resturns the response).
        /// Throws an exception if the response type is unexpected.
        /// </summary>
        private async Task<HttpResponseMessage> ReqGameStateUntilActiveOrFailure()
        {
            while (true)
            {
                var response = await boggleClient.GetAsync($"/BoggleService.svc/games/{gameId}");
                switch (response.StatusCode)
                {
                    case HttpStatusCode.Forbidden:
                        Debug.WriteLine($"Update game failed. gameId is invalid: {GetGameStateString()}");
                        return response;
                    case HttpStatusCode.OK:
                        dynamic deserialized = GetDeserializedResponseContent(response);
                        string gameState = deserialized.GameState;
                        if (gameState.Equals("active"))
                        {
                            return response;
                        }
                        if (gameState.Equals("pending"))
                        {
                            // Make another request.
                            break;
                        }
                        Debug.WriteLine("Unexpected gamestate: {gameState}");
                        break;
                    default:
                        return null;

                }
            }
        }

        /// <summary>
        /// Attempts to play the given word. If the word is valid, updates the gameview accordingly. Otherwise does nothing.
        /// </summary>
        private async void HandlePlayWord(string word)
        {
            Debug.Assert(IsGameStateValid(), GetGameStateString());

            if (string.IsNullOrEmpty(word))
            {
                // Illegal word. Ignore user's request.
                return;
            }

            dynamic reqBody = new ExpandoObject();
            reqBody.UserToken = userToken;
            reqBody.Word = word;
            StringContent reqJson = GetJsonForRequestBody(reqBody);

            try
            {
                var response = await boggleClient.PutAsync($"/BoggleService.svc/games/{gameId}", reqJson);
                switch (response.StatusCode)
                {
                    case HttpStatusCode.Forbidden:
                        // Illegal word. Ignore.
                        Debug.WriteLine("Attempted to play an invalid word: {0}", word);
                        return;
                    case HttpStatusCode.Conflict:
                        // Game state is inactive.
                        Debug.WriteLine("Attempted to play word when game state was inactive");
                        return;
                    case HttpStatusCode.OK:
                        string scoreStr = GetDeserializedResponseContent(response).Score;
                        int deserializedScore = int.Parse(scoreStr);
                        userScore += deserializedScore;
                        boardView.SetPlayerScore(userScore.Value);
                        boardView.SetPlayedWord(word, deserializedScore);
                        return;
                    default:
                        boardView.ShowServerError();
                        return;
                }
            }
            catch (TaskCanceledException)
            {
                // User canceled the attempt to play a word. Do nothing.
                return;
            }
         }

        /// <summary>
        /// Called periodically to request game updates from the server. Should the server respond that the
        /// game is completed, shows the endgame information.
        /// </summary>
        private async void RequestGameStatusUpdate()
        {
            if (!IsGameStateValid()) return; // Update attempted after user has cancelled game.
            try
            {
                var response = await boggleClient.GetAsync($"/BoggleService.svc/games/{gameId}");
                switch (response.StatusCode)
                {
                    case HttpStatusCode.Forbidden:
                        Debug.WriteLine($"Update game failed. gameId is invalid: {GetGameStateString()}");
                        return;
                    case HttpStatusCode.OK:
                        dynamic update = GetDeserializedResponseContent(response);
                        string gameState = update.GameState;
                        if (gameState.Equals("pending"))
                        {
                            // Do nothing.
                        }
                        else if (gameState.Equals("active"))
                        {
                            // Update both players' scores. Note that the user's score should already
                            // be up to date (from calls to HandlePlayWord). The opponent's score may not be.
                            string opponentScoreStr = ((string)update.Player1.Nickname).Equals(userNickname)
                                ? update.Player2.Score
                                : update.Player1.Score;
                            var opponentScoreInt = int.Parse(opponentScoreStr);
                            boardView.SetOpponentScore(opponentScoreInt);
                            string userScoreStr = ((string)update.Player1.Nickname).Equals(userNickname)
                                ? update.Player1.Score
                                : update.Player2.Score;
                            var userScoreInt = int.Parse(userScoreStr);
                            boardView.SetPlayerScore(userScoreInt);
                        }
                        else if (gameState.Equals("completed"))
                        {
                            HandleGameOver(update);
                        }
                        return;
                    default:
                        return;

                }
            }
            catch (Exception)
            {
                // User canceled pending requests. Do nothing.
                return;
            }
        }

        /// <summary>
        /// Takes the object representing the end-of-game gamestatus json returned by a boggle server.
        /// Orchestrates the end-of-game UI transition.
        /// </summary>
        private void HandleGameOver(dynamic gameStatus)
        {
            updateTimer.Stop();
            dynamic opponent = ((string)gameStatus.Player1.Nickname).Equals(userNickname)
                ? gameStatus.Player2
                : gameStatus.Player1;

            int opponentScore = int.Parse((string) opponent.Score);
            var opponentMoves = new List<KeyValuePair<string, int>>();
            foreach (dynamic wordPlayed in opponent.WordsPlayed)
            {
                string wordPlayedWordStr = ((string)wordPlayed.Word);
                int wordPlayedScoreInt = int.Parse(((string)wordPlayed.Score));
                opponentMoves.Add(new KeyValuePair<string, int>(wordPlayedWordStr, wordPlayedScoreInt));
            }
            boardView.ShowGameOverSummary(opponentMoves, opponentScore);
        }

        /// <summary>
        /// Closes the current gameview and requires the user to login again. 
        /// </summary>
        private void HandleRequestNewGame()
        {
            boardView.Hide();
            loginView.Hide();

            updateTimer?.Stop();

            NullifyGameState();

            boardView = new BoardForm();
            loginView = new LoginForm();
            InitSubscriptions();

            loginView.Show();
        }

        /// <summary>
        /// Stops all update activity and requests corresponding to the ongoing game.
        /// Nullifies all game state, including the boggle client.
        /// </summary>
        private void NullifyGameState()
        {
            updateTimer?.Stop();
            boggleClient?.CancelPendingRequests();

            boggleClient = null;

            updateTimer = null;
            userNickname = null;
            userToken = null;
            gameId = null;
        }

        /// <summary>
        /// Cancels any pending requests made to the boggle server. 
        /// </summary>
        private void HandleCancelLogin()
        {
            if (boggleClient != null)
            {
                boggleClient.CancelPendingRequests();
            }
            boardView.Hide();
            NullifyGameState();
            boardView = new BoardForm();
            InitBoardSubscriptions();
            loginView.Show();
            loginView.ShowCanceledLogin();
        }

        /// <summary>
        /// Cancels any pending request made by the player. In the current implementation, these can
        /// only include PlayWord requests.
        /// </summary>
        private void HandleCancelPlayWord()
        {
            boggleClient.CancelPendingRequests();
        }

        /// <summary>
        /// Returns whether all of the gamestate elements are non-null. 
        /// </summary>
        private bool IsGameStateValid()
        {
            return boggleClient != null &&
                   updateTimer != null &&
                   userToken != null &&
                   userNickname != null &&
                   userScore != null &&
                   gameId != null;
        }

        // For debugging. Returns a string representing the entirety of the gamestate.
        private string GetGameStateString()
        {
            return $"boggleClient: {boggleClient} " +
                   $"updateTimer: {updateTimer} " +
                   $"userToken: {userToken} " +
                   $"userNickname: {userNickname}" +
                   $"userScore: {userScore}" +
                   $"gameId: {gameId}";
        }

        private static StringContent GetJsonForRequestBody(dynamic reqBody)
        {
            return new StringContent(JsonConvert.SerializeObject(reqBody), 
                Encoding.UTF8, "application/json");
        }

        private static dynamic GetDeserializedResponseContent(HttpResponseMessage response)
        {
            var result = response.Content.ReadAsStringAsync().Result;
            dynamic deserialized = JsonConvert.DeserializeObject(result);
            return deserialized;
        }

    }
}
