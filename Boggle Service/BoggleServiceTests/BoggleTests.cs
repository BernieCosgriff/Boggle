using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Dynamic;
using static System.Net.HttpStatusCode;
using System.Diagnostics;
using System.Threading;

namespace Boggle
{
    /// <summary>
    /// Provides a way to start and stop the IIS web server from within the test
    /// cases.  If something prevents the test cases from stopping the web server,
    /// subsequent tests may not work properly until the stray process is killed
    /// manually.
    /// </summary>
    //public static class IISAgent
    //{
    //    // Reference to the running process
    //    private static Process process = null;

    //    /// <summary>
    //    /// Starts IIS
    //    /// </summary>
    //    public static void Start(string arguments)
    //    {
    //        if (process == null)
    //        {
    //            ProcessStartInfo info = new ProcessStartInfo(Properties.Resources.IIS_EXECUTABLE, arguments);
    //            info.WindowStyle = ProcessWindowStyle.Minimized;
    //            info.UseShellExecute = false;
    //            process = Process.Start(info);
    //        }
    //    }

    //    /// <summary>
    //    ///  Stops IIS
    //    /// </summary>
    //    public static void Stop()
    //    {
    //        if (process != null)
    //        {
    //            process.Kill();
    //        }
    //    }
    //}
    [TestClass]
    public class BoggleTests
    {
        ///// <summary>
        ///// This is automatically run prior to all the tests to start the server
        ///// </summary>
        //[ClassInitialize()]
        //public static void StartServer(TestContext context)
        //{
        //    WebServer.Main();
        //}

        ///// <summary>
        ///// This is automatically run when all tests have completed to stop the server
        ///// </summary>
        //[ClassCleanup()]
        //public static void StopIIS()
        //{
            
        //}

        private RestTestClient client = new RestTestClient("http://localhost:60000/");

        [TestMethod]
        public void Test_CreateUser_Returns403WhenNicknameIsNull()
        {
            dynamic user = new ExpandoObject();
            user.Nickname = null;
            Response response = client.DoPostAsync("users", user).Result;
            Assert.AreEqual(Forbidden, response.Status);
        }

        [TestMethod]
        public void Test_CreateUser_Returns403WhenNicknameIsEmptyString()
        {
            dynamic user = new ExpandoObject();
            user.Nickname = "";
            Response response = client.DoPostAsync("users", user).Result;
            Assert.AreEqual(Forbidden, response.Status);
        }

        [TestMethod]
        public void Test_CreateUser_Returns403WhenNicknameFieldIsNotPresentAtAll()
        {
            dynamic user = new ExpandoObject();
            Response response = client.DoPostAsync("users", user).Result;
            Assert.AreEqual(Forbidden, response.Status);
        }

        [TestMethod]
        public void Test_CreateUser_Returns201WithTokenWhenNicknameIsValid()
        {
            dynamic user = new ExpandoObject();
            user.Nickname = "Gilbert";
            Response response = client.DoPostAsync("users", user).Result;
            Assert.AreEqual(Created, response.Status);
            string s = response.Data.UserToken;
            Assert.IsInstanceOfType(s, typeof(string));
        }

        [TestMethod]
        public void Test_JoinGame_Returns403IfUserTokenIsInvalid()
        {
            dynamic game = new ExpandoObject();
            game.UserToken = "";
            game.TimeLimit = 30;
            Response response = client.DoPostAsync("games", game).Result;
            Assert.AreEqual(Forbidden, response.Status);
        }

        [TestMethod]
        public void Test_JoinGame_Returns403IfTimeLimitIsLessThan5()
        {
            dynamic user = new ExpandoObject();
            user.Nickname = "Joe";
            Response token = client.DoPostAsync("users", user).Result;
            dynamic game = new ExpandoObject();
            game.UserToken = token.Data.UserToken;
            game.TimeLimit = 3;
            Response response = client.DoPostAsync("games", game).Result;
            Assert.AreEqual(Forbidden, response.Status);
        }

        [TestMethod]
        public void Test_JoinGame_Returns403IfTimeLimitIsGreaterThan120()
        {
            dynamic user = new ExpandoObject();
            user.Nickname = "Joe";
            Response token = client.DoPostAsync("users", user).Result;
            dynamic game = new ExpandoObject();
            game.UserToken = token.Data.UserToken;
            game.TimeLimit = 121;
            Response response = client.DoPostAsync("games", game).Result;
            Assert.AreEqual(Forbidden, response.Status);
        }

        [TestMethod]
        public void Test_JoinGame_Returns409IfPlayerIsAlreadyInGame()
        {
            //Create user
            dynamic user = new ExpandoObject();
            user.Nickname = "Joe";

            //Get user token
            Response token = client.DoPostAsync("users", user).Result;
            dynamic o1 = token.Data;

            dynamic game = new ExpandoObject();
            game.UserToken = o1.UserToken;
            game.TimeLimit = 40;

            Response response = client.DoPostAsync("games", game).Result;
            Assert.AreEqual(Accepted, response.Status);

            Response response2 = client.DoPostAsync("games", game).Result;
            Assert.AreEqual(Conflict, response2.Status);

            dynamic cancelInfo = new ExpandoObject();
            cancelInfo.UserToken = o1.UserToken;

            client.DoPutAsync(cancelInfo, "games");
        }

        [TestMethod]
        public void Test_JoinGame_CreatesActiveGameWhenTwoPlayeresArePresent()
        {
            //Create player1
            dynamic player1 = new ExpandoObject();
            player1.Nickname = "Bernie";

            //Get their UserToken
            Response token1 = client.DoPostAsync("users", player1).Result;
            Assert.AreEqual(Created, token1.Status);

            dynamic o1 = token1.Data;

            //Create player 2
            dynamic player2 = new ExpandoObject();
            player2.Nickname = "Alex";

            //Get their UserToken
            Response token2 = client.DoPostAsync("users", player2).Result;
            Assert.AreEqual(Created, token2.Status);

            dynamic o2 = token2.Data;

            //Create gameInfo for player1
            dynamic gameInfo1 = new ExpandoObject();
            gameInfo1.UserToken = o1.UserToken;
            gameInfo1.TimeLimit = 30;

            //Create gameInfo for player2
            dynamic gameInfo2 = new ExpandoObject();
            gameInfo2.UserToken = o2.UserToken;
            gameInfo2.TimeLimit = 30;

            //Player1 join game
            Response response1 = client.DoPostAsync("games", gameInfo1).Result;
            Assert.AreEqual(Accepted, response1.Status);

            //Player2 join game
            Response response2 = client.DoPostAsync("games", gameInfo2).Result;
            Assert.AreEqual(Created, response2.Status);

            dynamic o3 = response1.Data;

            //Get the game status
            string[] s = new string[0];
            Response gStatus = client.DoGetAsync("games/" + o3.GameID, s).Result;
            Assert.AreEqual(OK, gStatus.Status);
        }

        [TestMethod]
        public void Test_JoinGame_AddsPlayerToPendingGameIfNoOtherPlayersAreWaiting()
        {
            //Create player1
            dynamic player1 = new ExpandoObject();
            player1.Nickname = "Bernie";

            //Get their UserToken
            Response token1 = client.DoPostAsync("users", player1).Result;
            Assert.AreEqual(Created, token1.Status);

            dynamic o1 = token1.Data;

            //Create gameInfo for player1
            dynamic gameInfo1 = new ExpandoObject();
            gameInfo1.UserToken = o1.UserToken;
            gameInfo1.TimeLimit = 30;

            //Player1 join game
            Response response1 = client.DoPostAsync("games", gameInfo1).Result;
            Assert.AreEqual(Accepted, response1.Status);

            dynamic cancelInfo = new ExpandoObject();
            cancelInfo.UserToken = o1.UserToken;

            client.DoPutAsync(cancelInfo, "games");
        }

        [TestMethod]
        public void Test_JoinGame_403IfUserTokenInvalid()
        {
            //Create player1
            dynamic player1 = new ExpandoObject();
            player1.Nickname = "Bernie";

            //Get UserToken
            Response token1 = client.DoPostAsync("users", player1).Result;
            Assert.AreEqual(Created, token1.Status);

            //Create gameInfo w/ null UserToken
            dynamic gameInfo1 = new ExpandoObject();
            gameInfo1.UserToken = null;
            gameInfo1.TimeLimit = 30;

            //Join game
            Response response1 = client.DoPostAsync("games", gameInfo1).Result;
            Assert.AreEqual(Forbidden, response1.Status);

            //Invalid UserToken
            gameInfo1.UserToken = "hello";

            //Join game
            Response response2 = client.DoPostAsync("games", gameInfo1).Result;
            Assert.AreEqual(Forbidden, response2.Status);
        }

        [TestMethod]
        public void Test_PlayWord_200IfValid()
        {
            //Create p1
            dynamic player1 = new ExpandoObject();
            player1.Nickname = "Bernie";

            //Get UserToken
            Response token1 = client.DoPostAsync("users", player1).Result;
            Assert.AreEqual(Created, token1.Status);

            dynamic o1 = token1.Data;

            //Create p2
            dynamic player2 = new ExpandoObject();
            player2.Nickname = "Alex";

            //Get UserToken
            Response token2 = client.DoPostAsync("users", player2).Result;
            Assert.AreEqual(Created, token2.Status);

            dynamic o2 = token2.Data;

            //Create p1 gameInfo
            dynamic gameInfo1 = new ExpandoObject();
            gameInfo1.UserToken = o1.UserToken;
            gameInfo1.TimeLimit = 30;

            //Create p2 gameInfo
            dynamic gameInfo2 = new ExpandoObject();
            gameInfo2.UserToken = o2.UserToken;
            gameInfo2.TimeLimit = 30;

            //p1 join game
            Response response1 = client.DoPostAsync("games", gameInfo1).Result;
            Assert.AreEqual(Accepted, response1.Status);

            //p2 join game
            Response response2 = client.DoPostAsync("games", gameInfo2).Result;
            Assert.AreEqual(Created, response2.Status);

            dynamic o3 = response1.Data;

            //Get game status
            string[] s = new string[0];
            Response gStatus = client.DoGetAsync("games/" + o3.GameID, s).Result;
            Assert.AreEqual(OK, gStatus.Status);

            //Create play word info
            dynamic playInfo = new ExpandoObject();
            playInfo.UserToken = gameInfo1.UserToken;
            playInfo.Word = "hello";

            //play word
            Response play = client.DoPutAsync(playInfo, "games/" + o3.GameID).Result;
            Assert.AreEqual(OK, play.Status);
        }
        [TestMethod]
        public void Test_PlayWord_PlayAllWords()
        {

            //Create p1
            dynamic player1 = new ExpandoObject();
            player1.Nickname = "Bernie";

            //Get UserToken
            Response token1 = client.DoPostAsync("users", player1).Result;
            Assert.AreEqual(Created, token1.Status);

            dynamic o1 = token1.Data;

            //Create p2
            dynamic player2 = new ExpandoObject();
            player2.Nickname = "Alex";

            //Get UserToken
            Response token2 = client.DoPostAsync("users", player2).Result;
            Assert.AreEqual(Created, token2.Status);

            dynamic o2 = token2.Data;

            //Create p1 gameInfo
            dynamic gameInfo1 = new ExpandoObject();
            gameInfo1.UserToken = o1.UserToken;
            gameInfo1.TimeLimit = 5;

            //Create p2 gameInfo
            dynamic gameInfo2 = new ExpandoObject();
            gameInfo2.UserToken = o2.UserToken;
            gameInfo2.TimeLimit = 5;

            //p1 join game
            Response response1 = client.DoPostAsync("games", gameInfo1).Result;
            Assert.AreEqual(Accepted, response1.Status);

            //p2 join game
            Response response2 = client.DoPostAsync("games", gameInfo2).Result;
            Assert.AreEqual(Created, response2.Status);

            dynamic o3 = response1.Data;

            //Get game status
            Response gStatus = client.DoGetAsync("games/" + o3.GameID).Result;
            Assert.AreEqual(OK, gStatus.Status);

            //Create play word info
            dynamic playInfo = new ExpandoObject();
            playInfo.UserToken = gameInfo1.UserToken;

            //string ID = gameInfo1.UserToken;

            //foreach (string str in BoggleService.GetAllLegalWordsForPendingGame(ID))
            //{
            //    playInfo.Word = str;
            //    Response r = client.DoPutAsync(playInfo, "games/" + o3.GameID).Result;
            //    Assert.AreEqual(OK, r.Status);
            //}
            //response1 = client.DoGetAsync("games/" + o3.GameID).Result;
            //Assert.AreEqual(OK, response1.Status);

            //Thread.Sleep(5000);

            //response1 = client.DoGetAsync("games/" + o3.GameID, "yes").Result;
            //Assert.AreEqual(OK, response1.Status);
        }

        [TestMethod]
        public void Test_PlayWord_403IfWordNullOrEmpty()
        {
            //Create p1
            dynamic player1 = new ExpandoObject();
            player1.Nickname = "Bernie";

            //Get UserToken
            Response token1 = client.DoPostAsync("users", player1).Result;
            Assert.AreEqual(Created, token1.Status);

            dynamic o1 = token1.Data;

            //Create p2
            dynamic player2 = new ExpandoObject();
            player2.Nickname = "Alex";

            //Get UserToken
            Response token2 = client.DoPostAsync("users", player2).Result;
            Assert.AreEqual(Created, token2.Status);

            dynamic o2 = token2.Data;

            //Create p1 gameInfo
            dynamic gameInfo1 = new ExpandoObject();
            gameInfo1.UserToken = o1.UserToken;
            gameInfo1.TimeLimit = 30;

            //Create p2 gameInfo
            dynamic gameInfo2 = new ExpandoObject();
            gameInfo2.UserToken = o2.UserToken;
            gameInfo2.TimeLimit = 30;

            //p1 join game
            Response response1 = client.DoPostAsync("games", gameInfo1).Result;
            Assert.AreEqual(Accepted, response1.Status);

            //p2 join game
            Response response2 = client.DoPostAsync("games", gameInfo2).Result;
            Assert.AreEqual(Created, response2.Status);

            dynamic o3 = response1.Data;

            //Get game status
            string[] s = new string[0];
            Response gStatus = client.DoGetAsync("games/" + o3.GameID, s).Result;
            Assert.AreEqual(OK, gStatus.Status);

            //Create play word info
            dynamic playInfo = new ExpandoObject();
            playInfo.UserToken = gameInfo1.UserToken;
            playInfo.Word = "";

            //play word
            Response play = client.DoPutAsync(playInfo, "games/" + o3.GameID).Result;
            Assert.AreEqual(Forbidden, play.Status);

            playInfo.Word = null;

            //play word
            Response play1 = client.DoPutAsync(playInfo, "games/" + o3.GameID).Result;
            Assert.AreEqual(Forbidden, play.Status);
        }

        [TestMethod]
        public void Test_PlayWord_409IfGameNotActive()
        {

            //Create p1
            dynamic player1 = new ExpandoObject();
            player1.Nickname = "Bernie";

            //Get id
            Response token1 = client.DoPostAsync("users", player1).Result;
            Assert.AreEqual(Created, token1.Status);

            dynamic o1 = token1.Data;

            dynamic gameInfo1 = new ExpandoObject();
            gameInfo1.UserToken = o1.UserToken;
            gameInfo1.TimeLimit = 30;

            //Get id
            Response response1 = client.DoPostAsync("games", gameInfo1).Result;
            Assert.AreEqual(Accepted, response1.Status);

            //Create play word info
            dynamic playInfo = new ExpandoObject();
            playInfo.UserToken = o1.UserToken;
            playInfo.Word = "valid";

            dynamic o2 = response1.Data;

            //Play word
            Response play = client.DoPutAsync(playInfo, "games/" + o2.GameID).Result;
            Assert.AreEqual(Conflict, play.Status);

            dynamic cancelInfo = new ExpandoObject();
            cancelInfo.UserToken = o1.UserToken;

            client.DoPutAsync(cancelInfo, "games");
        }

        [TestMethod]
        public void Test_GameStatus_403InvalidGameID()
        {
            //Create p1
            dynamic player1 = new ExpandoObject();
            player1.Nickname = "Bernie";

            //Get id
            Response token1 = client.DoPostAsync("users", player1).Result;
            Assert.AreEqual(Created, token1.Status);

            string[] s = new string[0];
            Response gStatus = client.DoGetAsync("games/ardvark", s).Result;
            Assert.AreEqual(Forbidden, gStatus.Status);
        }

        [TestMethod]
        public void Test_JoinGame_CancelJoinRequest()
        {
            //Create player1
            dynamic player1 = new ExpandoObject();
            player1.Nickname = "Bernie";

            //Get their UserToken
            Response token1 = client.DoPostAsync("users", player1).Result;
            Assert.AreEqual(Created, token1.Status);

            dynamic o1 = token1.Data;

            //Create gameInfo for player1
            dynamic gameInfo1 = new ExpandoObject();
            gameInfo1.UserToken = o1.UserToken;
            gameInfo1.TimeLimit = 30;

            //Player1 join game
            Response response1 = client.DoPostAsync("games", gameInfo1).Result;
            Assert.AreEqual(Accepted, response1.Status);

            //Create cancel game info
            dynamic cancelInfo = new ExpandoObject();
            cancelInfo.UserToken = o1.UserToken;

            //Cancel join game request
            Response r2 = client.DoPutAsync(cancelInfo, "games").Result;
            Assert.AreEqual(OK, r2.Status);

            dynamic player2 = new ExpandoObject();
            player2.Nickname = "T";

            Response token2 = client.DoPostAsync("users", player2).Result;
            Assert.AreEqual(Created, token2.Status);

            dynamic o2 = token2.Data;

            dynamic gameInfo2 = new ExpandoObject();
            gameInfo1.UserToken = o2.UserToken;
            gameInfo1.TimeLimit = 30;

            // Player2 join game
            Response response2 = client.DoPostAsync("games", gameInfo1).Result;
            Assert.AreEqual(Accepted, response2.Status);

            cancelInfo.UserToken = o2.UserToken;

            Response cancel = client.DoPutAsync(cancelInfo, "games").Result;
            Assert.AreEqual(OK, cancel.Status);
        }

        [TestMethod]
        public void Test_JoinGame_CancelJoinRequestInvalidToken()
        {
            //Create player1
            dynamic player1 = new ExpandoObject();
            player1.Nickname = "Bernie";

            //Get their UserToken
            Response token1 = client.DoPostAsync("users", player1).Result;
            Assert.AreEqual(Created, token1.Status);

            dynamic o1 = token1.Data;

            //Create gameInfo for player1
            dynamic gameInfo1 = new ExpandoObject();
            gameInfo1.UserToken = o1.UserToken;
            gameInfo1.TimeLimit = 30;

            //Player1 join game
            Response response1 = client.DoPostAsync("games", gameInfo1).Result;
            Assert.AreEqual(Accepted, response1.Status);

            //Create cancel game info
            dynamic cancelInfo = new ExpandoObject();
            cancelInfo.UserToken = 777;

            //Cancel join game request
            Response r2 = client.DoPutAsync(cancelInfo, "games").Result;
            Assert.AreEqual(Forbidden, r2.Status);

            cancelInfo.UserToken = o1.UserToken;

            //Cancel join game request
            r2 = client.DoPutAsync(cancelInfo, "games").Result;
            Assert.AreEqual(OK, r2.Status);
        }

        [TestMethod]
        public void Test_JoinGame_CancelJoinRequestNullOrEmptyToken()
        {
            //Create player1
            dynamic player1 = new ExpandoObject();
            player1.Nickname = "Bernie";

            //Get their UserToken
            Response token1 = client.DoPostAsync("users", player1).Result;
            Assert.AreEqual(Created, token1.Status);

            dynamic o1 = token1.Data;

            //Create gameInfo for player1
            dynamic gameInfo1 = new ExpandoObject();
            gameInfo1.UserToken = o1.UserToken;
            gameInfo1.TimeLimit = 30;

            //Player1 join game
            Response response1 = client.DoPostAsync("games", gameInfo1).Result;
            Assert.AreEqual(Accepted, response1.Status);

            //Create cancel game info
            dynamic cancelInfo = new ExpandoObject();
            cancelInfo.UserToken = null;

            //Cancel join game request
            Response r2 = client.DoPutAsync(cancelInfo, "games").Result;
            Assert.AreEqual(Forbidden, r2.Status);

            cancelInfo.UserToken = "";

            //Cancel join game request
            r2 = client.DoPutAsync(cancelInfo, "games").Result;
            Assert.AreEqual(Forbidden, r2.Status);

            cancelInfo.UserToken = o1.UserToken;

            //Cancel join game request
            r2 = client.DoPutAsync(cancelInfo, "games").Result;
            Assert.AreEqual(OK, r2.Status);
        }

        [TestMethod]
        public void Test_GameStatus_200GameActive()
        {

            //Create player1
            dynamic player1 = new ExpandoObject();
            player1.Nickname = "Bernie";

            //Get their UserToken
            Response token1 = client.DoPostAsync("users", player1).Result;
            Assert.AreEqual(Created, token1.Status);

            //Create player 2
            dynamic player2 = new ExpandoObject();
            player2.Nickname = "Alex";

            //Get their UserToken
            Response token2 = client.DoPostAsync("users", player2).Result;
            Assert.AreEqual(Created, token2.Status);

            dynamic s1 = token1.Data.UserToken;
            dynamic s2 = token2.Data.UserToken;

            //Create gameInfo for player1
            dynamic gameInfo1 = new ExpandoObject();
            gameInfo1.UserToken = s1;
            gameInfo1.TimeLimit = 39;

            //Create gameInfo for player2
            dynamic gameInfo2 = new ExpandoObject();
            gameInfo2.UserToken = s2;
            gameInfo2.TimeLimit = 30;

            //Player1 join game
            Response response1 = client.DoPostAsync("games", gameInfo1).Result;
            Assert.AreEqual(Accepted, response1.Status);

            //Player2 join game
            Response response2 = client.DoPostAsync("games", gameInfo2).Result;
            Assert.AreEqual(Created, response2.Status);

            dynamic o3 = response1.Data.GameID;

            //Get the game status
            string[] s = new string[0];
            Response gStatus = client.DoGetAsync("games/" + o3, s).Result;
            Assert.AreEqual(OK, gStatus.Status);
        }

        [TestMethod]
        public void Test_GameStatus_200GamePending()
        {
            //Create player1
            dynamic player1 = new ExpandoObject();
            player1.Nickname = "Bernie";

            //Get their UserToken
            Response token1 = client.DoPostAsync("users", player1).Result;
            Assert.AreEqual(Created, token1.Status);
            
            dynamic jobj = token1.Data;

            //Create gameInfo for player1
            dynamic gameInfo1 = new ExpandoObject();
            gameInfo1.UserToken = jobj.UserToken;
            gameInfo1.TimeLimit = 30;

            //Player1 join game
            Response response1 = client.DoPostAsync("games", gameInfo1).Result;
            Assert.AreEqual(Accepted, response1.Status);

            dynamic jobj2 = response1.Data;

            //Get the game status
            string[] s = new string[0];
            Response gStatus = client.DoGetAsync("games/" + jobj2.GameID, s).Result;
            Assert.AreEqual(OK, gStatus.Status);

            dynamic cancelInfo = new ExpandoObject();
            cancelInfo.UserToken = jobj.UserToken;

            client.DoPutAsync(cancelInfo, "games");
        }

        [TestMethod]
        public void Test_GameStatus_200GameActiveBrief()
        {
            //Create player1
            dynamic player1 = new ExpandoObject();
            player1.Nickname = "Bernie";

            //Get their UserToken
            Response token1 = client.DoPostAsync("users", player1).Result;
            Assert.AreEqual(Created, token1.Status);

            dynamic o1 = token1.Data;

            //Create player 2
            dynamic player2 = new ExpandoObject();
            player2.Nickname = "Alex";

            //Get their UserToken
            Response token2 = client.DoPostAsync("users", player2).Result;
            Assert.AreEqual(Created, token2.Status);

            dynamic o2 = token2.Data;

            //Create gameInfo for player1
            dynamic gameInfo1 = new ExpandoObject();
            gameInfo1.UserToken = o1.UserToken;
            gameInfo1.TimeLimit = 30;

            //Create gameInfo for player2
            dynamic gameInfo2 = new ExpandoObject();
            gameInfo2.UserToken = o2.UserToken;
            gameInfo2.TimeLimit = 30;

            //Player1 join game
            Response response1 = client.DoPostAsync("games", gameInfo1).Result;
            Assert.AreEqual(Accepted, response1.Status);

            //Player2 join game
            Response response2 = client.DoPostAsync("games", gameInfo2).Result;
            Assert.AreEqual(Created, response2.Status);

            string o3 = response1.Data.GameID;

            Console.WriteLine("GID: " + o3);

            //Get the game status
            Response gStatus = client.DoGetAsync("games/" + o3 + "?brief={0}", "yes").Result;
            Assert.AreEqual(OK, gStatus.Status);

            dynamic cancelInfo = new ExpandoObject();
        }

        [TestMethod]
        public void Test_GameStatus_200GamePendingBrief()
        {
            //Create player1
            dynamic player1 = new ExpandoObject();
            player1.Nickname = "Bernie";

            //Get their UserToken
            Response token1 = client.DoPostAsync("users", player1).Result;
            Assert.AreEqual(Created, token1.Status);

            dynamic o2 = token1.Data;

            //Create gameInfo for player1
            dynamic gameInfo1 = new ExpandoObject();
            gameInfo1.UserToken = o2.UserToken;
            gameInfo1.TimeLimit = 30;

            //Player1 join game
            Response response1 = client.DoPostAsync("games", gameInfo1).Result;
            Assert.AreEqual(Accepted, response1.Status);

            dynamic o1 = response1.Data;

            //Get the game status
            Response gStatus = client.DoGetAsync("games/" + o1.GameID + "?brief={0}", "yes").Result;
            Assert.AreEqual(OK, gStatus.Status);

            dynamic cancelInfo = new ExpandoObject();
            cancelInfo.UserToken = o2.UserToken;

            client.DoPutAsync(cancelInfo, "games");
        }
    }
}
