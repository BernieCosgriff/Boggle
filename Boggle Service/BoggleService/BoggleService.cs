using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.ServiceModel.Web;
using static System.Net.HttpStatusCode;
using System.Linq;
using System.Configuration;

namespace Boggle
{
    /// <summary>
    /// A boggle http service. 
    /// </summary>
    public class BoggleService : IBoggleService
    {

        // An in-memory store of legal words. 
        private static readonly HashSet<string> dictionary = ReadInDictionary(); 

        // The database connection string.
        public static readonly string BoggleDbConnString = ConfigurationManager.ConnectionStrings["BoggleDbConnString"].ConnectionString;

        /// <summary>
        /// Returns a Stream version of index.html.
        /// </summary>
        /// <returns></returns>
        public Tuple<HttpStatusCode, string> API()
        {
            return Tuple.Create(HttpStatusCode.OK, File.ReadAllText(Path.Combine(Environment.CurrentDirectory, "index.html")));
        }

        /// <summary>
        /// Responds to a user's request to join the service. 
        /// </summary>
        public Tuple<HttpStatusCode, Token> CreateUser(UserInfo user)
        {
            Debug.WriteLine($"CreateUser called with nickname {user.Nickname}");
            if (string.IsNullOrWhiteSpace(user.Nickname))
            {
                return new Tuple<HttpStatusCode, Token>(Forbidden, null);
            }

            var userToken = Guid.NewGuid().ToString();

            using (SqlConnection conn = new SqlConnection(BoggleDbConnString))
            {
                conn.Open();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    using (SqlCommand command = new SqlCommand(
                        "insert into Users (UserID, Nickname) values(@UserID, @Nickname)", conn, trans))
                    {
                        command.Parameters.AddWithValue("@UserID", userToken);
                        command.Parameters.AddWithValue("@Nickname", user.Nickname.Trim());

                        int rowsAffected = command.ExecuteNonQuery();

                        Debug.Assert(rowsAffected == 1, $"Adding a user to the database should alter 1 row. Instead, it altered {rowsAffected}." +
                            $"\nUserNickname:{user.Nickname}, UserToken: {userToken}");
                    }
                    trans.Commit();
                }
            }

            // SetStatus(Created);
            return Tuple.Create(HttpStatusCode.Created, new Token()
            {
                UserToken = userToken
            });
        }

        /// <summary>
        /// Responds to a user's request to join a game. 
        /// </summary>
        public Tuple<HttpStatusCode, GameIdInfo> JoinGame(JoinGameInfo joinGameInfo)
        {
            Debug.WriteLine($"Join game called with user token {joinGameInfo.UserToken} and timeLimit {joinGameInfo.TimeLimit}");
            int? gameID = null;
            int? pendingTime = null;
            HttpStatusCode responseCode;
            if (string.IsNullOrEmpty(joinGameInfo.UserToken) ||
                 joinGameInfo.TimeLimit < 5 ||
                 joinGameInfo.TimeLimit > 120)
            {
                return new Tuple<HttpStatusCode, GameIdInfo>(Forbidden, null);
            }

            using (SqlConnection conn = new SqlConnection(BoggleDbConnString))
            {
                conn.Open();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    using (SqlCommand command =
                            new SqlCommand("select UserID from Users where UserID = @ID", conn, trans))
                    {
                        command.Parameters.AddWithValue("@ID", joinGameInfo.UserToken);
                        if ((string)command.ExecuteScalar() != joinGameInfo.UserToken)
                        {
                            return new Tuple<HttpStatusCode, GameIdInfo>(Forbidden, null);
                        }
                    }
                    using (SqlCommand command =
                            new SqlCommand("select Player1, GameID, TimeLimit from Games where Player2 is null", conn, trans))
                    {
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                if ((string)reader["Player1"] == joinGameInfo.UserToken)
                                {
                                    return new Tuple<HttpStatusCode, GameIdInfo>(Conflict, null);
                                }

                                gameID = (int?)reader["GameID"];
                                pendingTime = (int?)reader["TimeLimit"];
                            }
                        }
                    }
                    if (gameID != null && pendingTime != null)
                    {
                        using (SqlCommand command = new SqlCommand(
                            "update Games set Player2=@p2, Board=@board, TimeLimit=@limit, StartTime=@start where Player2 is null AND GameID=@id", conn, trans))
                        {
                            var board = new BoggleBoard().ToString();
                            var timeLimit = (pendingTime + joinGameInfo.TimeLimit) / 2;
                            var startTime = DateTime.Now;
                            command.Parameters.AddWithValue("@p2", joinGameInfo.UserToken);
                            command.Parameters.AddWithValue("@board", board);
                            command.Parameters.AddWithValue("@limit", timeLimit);
                            command.Parameters.AddWithValue("@start", startTime);
                            command.Parameters.AddWithValue("@id", gameID);

                            var rowsAffected = command.ExecuteNonQuery();

                            Debug.Assert(rowsAffected == 1, $"Attempted to update a game to include a new player, but rows affected in db was {rowsAffected}" +
                                $"\nToken of New User:{joinGameInfo.UserToken}, board:{board}, timeLimit:{timeLimit}, startTime:{startTime}, gameID:{gameID}");

                            responseCode = Created;
                        }
                    }
                    else
                    {
                        using (SqlCommand command = new SqlCommand(
                            "insert into Games (Player1, TimeLimit) output inserted.GameID values (@player1, @timeLimit)", conn, trans))
                        {
                            command.Parameters.AddWithValue("@player1", joinGameInfo.UserToken);
                            command.Parameters.AddWithValue("@timeLimit", joinGameInfo.TimeLimit);

                            gameID = ((int?)command.ExecuteScalar()).Value;

                            Debug.WriteLine($"GameID of game created during call to JoinGame: {gameID}");

                            responseCode = Accepted;
                        }
                    }
                    trans.Commit();
                }
            }
            return Tuple.Create(responseCode, new GameIdInfo()
            {
                GameID = (int) gameID
            });
        }

        public HttpStatusCode CancelJoinRequest(CancelInfo cancelInfo)
        {
            Debug.WriteLine($"Cancel join request called with user token {cancelInfo.UserToken}");
            if (string.IsNullOrEmpty(cancelInfo.UserToken))
            {
                return Forbidden;
            }
            using (SqlConnection conn = new SqlConnection(BoggleDbConnString))
            {
                conn.Open();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    using (SqlCommand command = new SqlCommand("delete from Games where Player1 = @player1 AND Player2 IS NULL", conn, trans))
                    {
                        command.Parameters.AddWithValue("@player1", cancelInfo.UserToken);
                        if (command.ExecuteNonQuery() == 0)
                        {
                            trans.Commit();
                            return Forbidden;
                        }
                        else
                        {
                            trans.Commit();
                            return OK;
                        }
                    }
                }
            }
        }

        public Tuple<HttpStatusCode, PlayedWordScore> PlayWord(string gameIdStr, WordToPlayInfo wordToPlayInfo)
        {
            int gameId;
            if (string.IsNullOrEmpty(gameIdStr) ||
                string.IsNullOrEmpty(wordToPlayInfo.UserToken) ||
                string.IsNullOrEmpty(wordToPlayInfo.Word) ||
                string.IsNullOrWhiteSpace(wordToPlayInfo.Word) ||
                !int.TryParse(gameIdStr, out gameId))
            {
                return new Tuple<HttpStatusCode, PlayedWordScore>(Forbidden, null);
            }
            using (SqlConnection conn = new SqlConnection(BoggleDbConnString))
            {
                conn.Open();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    string player1Id;
                    string player2Id;
                    string board;
                    int? timeLimit;
                    DateTime startTime;
                    using (SqlCommand command = new SqlCommand(
                        "select Player1, Player2, Board, TimeLimit, StartTime from Games where GameID = @gameId", conn, trans))
                    {
                        command.Parameters.AddWithValue("@gameId", gameId);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            // If no game matches the given gameID, set status to forbidden.
                            if (!reader.Read())
                            {
                                return new Tuple<HttpStatusCode, PlayedWordScore>(Forbidden, null);
                            }

                            player1Id = reader[0] as string;
                            player2Id = reader[1] as string;
                            board = reader[2] as string;
                            timeLimit = reader[3] as int?;
                            startTime = reader[4] as DateTime? ?? default(DateTime);

                            // If the game does not include the player, set status to forbidden.
                            if (wordToPlayInfo.UserToken != player1Id &&
                                wordToPlayInfo.UserToken != player2Id)
                            {
                                return new Tuple<HttpStatusCode, PlayedWordScore>(Forbidden, null);
                            }
                            // If the game is pending (i.e. player2Id is null) or completed, set status to conflict. 
                            if ((player2Id == null && wordToPlayInfo.UserToken == player1Id) ||
                                startTime.AddSeconds(Convert.ToDouble(timeLimit)).CompareTo(DateTime.Now) < 0)
                            {
                                return new Tuple<HttpStatusCode, PlayedWordScore>(Conflict, null);
                            }
                        }
                    }

                    // If the player has already played the same word in this game, do not insert it into the database.
                    using (SqlCommand command = new SqlCommand(
                        "select Word from Words where GameID = @gameId AND Player = @playerId", conn, trans))
                    {
                        command.Parameters.AddWithValue("@gameId", gameId);
                        command.Parameters.AddWithValue("@playerId", wordToPlayInfo.UserToken);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            var lowerCasePlayedWord = wordToPlayInfo.Word.ToLower();
                            while (reader.Read())
                            {
                                if (reader.GetString(0).ToLower() == lowerCasePlayedWord)
                                {
                                    // The player has already played the word. 
                                    reader.Close();
                                    trans.Commit();
                                    return Tuple.Create(OK, new PlayedWordScore()
                                    {
                                        Score = 0
                                    });
                                }
                            }
                        }
                    }

                    // The player has not played the word before. Get the score for the word and insert the word info into the database.
                    var scoreForWord = new BoggleBoard(board).GetScoreForWord(wordToPlayInfo.Word, dictionary);
                    using (SqlCommand command = new SqlCommand(
                        "insert into Words (Word, GameID, Player, Score) values (@word, @gameId, @userToken, @score)", conn, trans))
                    {
                        command.Parameters.AddWithValue("@word", wordToPlayInfo.Word);
                        command.Parameters.AddWithValue("@gameId", gameId);
                        command.Parameters.AddWithValue("@userToken", wordToPlayInfo.UserToken);
                        command.Parameters.AddWithValue("@score", scoreForWord);

                        int rowsAffected = command.ExecuteNonQuery();
                        Debug.Assert(rowsAffected != 0, "Tried to insert a word into database but no rows were affected." +
                            $"\nWord:{wordToPlayInfo.Word},GameID:{gameId},UserToken:{wordToPlayInfo.UserToken},ScoreForWord{scoreForWord}");
                    }

                    trans.Commit();
                    return Tuple.Create(OK, new PlayedWordScore()
                    {
                        Score = scoreForWord
                    });
                }
            }
        }

        /// <summary>
        /// Responds to a request for a game status update. 
        /// </summary>
        public Tuple<HttpStatusCode, Game> GameStatus(string gameIdStr, string brief)
        {
            Debug.WriteLine($"GameStatus called with gameId {gameIdStr} and brief {brief}");
            int gameId;
            if (string.IsNullOrEmpty(gameIdStr) ||
                !int.TryParse(gameIdStr, out gameId))
            {
                return new Tuple<HttpStatusCode, Game>(Forbidden, null);
            }
            using (SqlConnection conn = new SqlConnection(BoggleDbConnString))
            {
                conn.Open();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    string player1Id;
                    string player2Id;
                    string board;
                    int? timeLimit;
                    DateTime startTime;
                    using (SqlCommand command = new SqlCommand(
                                "Select Player1, Player2, Board, TimeLimit, StartTime from Games where GameID = @gameId", conn, trans))
                    {
                        command.Parameters.AddWithValue("@gameId", gameId);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (!reader.Read())
                            {
                                reader.Close();
                                trans.Commit();
                                return new Tuple<HttpStatusCode, Game>(Forbidden, null);
                            }

                            player1Id = reader[0] as string;
                            player2Id = reader[1] as string;
                            board = reader[2] as string;
                            timeLimit = reader[3] as int?;
                            startTime = reader[4] as DateTime? ?? default(DateTime);
                        }
                    }

                    if (player2Id == null)
                    {
                        trans.Commit();
                        return Tuple.Create(OK, new Game
                        {
                            GameState = "pending"
                        });
                    }

                    string gameState = startTime.AddSeconds(Convert.ToDouble(timeLimit)).CompareTo(DateTime.Now) < 0 ? "completed" : "active";
                    long timeLeft = gameState == "completed"
                        ? 0
                        : Convert.ToInt64(startTime.AddSeconds(Convert.ToDouble(timeLimit)).Subtract(DateTime.Now).TotalSeconds);

                    int player1Score;
                    using (SqlCommand command = new SqlCommand(
                        "Select SUM(Score) as scoreSum from Words where GameID = @gameId AND Player = @player", conn, trans))
                    {
                        command.Parameters.AddWithValue("@gameId", gameId);
                        command.Parameters.AddWithValue("@player", player1Id);
                        var result = command.ExecuteScalar();
                        if (result == DBNull.Value) player1Score = 0;
                        else Debug.Assert(Int32.TryParse(result.ToString(), out player1Score),
                            $"Attempt to retrieve player1 score failed. Query result was not an integer. Query Result: {result}");
                    }

                    int player2Score;
                    using (SqlCommand command = new SqlCommand(
                        "Select SUM(Score) as scoreSum from Words where GameID = @gameId and Player = @player", conn, trans))
                    {
                        command.Parameters.AddWithValue("@gameId", gameId);
                        command.Parameters.AddWithValue("@player", player2Id);
                        var result = command.ExecuteScalar();
                        if (result == DBNull.Value) player2Score = 0;
                        else Debug.Assert(Int32.TryParse(result.ToString(), out player2Score),
                            $"Attempt to retrieve player2 score failed. Query result was not an integer. Query Result: {result}");
                    }

                    if (!string.IsNullOrEmpty(brief) && brief == "yes")
                    {
                        trans.Commit();
                        return Tuple.Create(OK, new Game
                        {
                            GameState = gameState,
                            TimeLeft = timeLeft,
                            Player1 = new Player()
                            {
                                Score = player1Score,
                                WordsPlayed = null
                            },
                            Player2 = new Player()
                            {
                                Score = player2Score,
                                WordsPlayed = null
                            }
                        });
                    }

                    string player1Nickname;
                    using (SqlCommand command = new SqlCommand(
                        "Select Nickname from Users where UserID = @userId", conn, trans))
                    {
                        command.Parameters.AddWithValue("@userId", player1Id);
                        player1Nickname = (string)command.ExecuteScalar();
                    }

                    string player2Nickname;
                    using (SqlCommand command = new SqlCommand(
                        "Select Nickname from Users where UserID = @userId", conn, trans))
                    {
                        command.Parameters.AddWithValue("@userId", player2Id);
                        player2Nickname = (string)command.ExecuteScalar();
                    }

                    if (gameState == "active")
                    {
                        trans.Commit();
                        return Tuple.Create(OK, new Game
                        {
                            GameState = gameState,
                            Board = board,
                            TimeLimit = timeLimit,
                            TimeLeft = timeLeft,
                            Player1 = new Player()
                            {
                                Nickname = player1Nickname,
                                Score = player1Score,
                                WordsPlayed = null
                            },
                            Player2 = new Player()
                            {
                                Nickname = player2Nickname,
                                Score = player2Score,
                                WordsPlayed = null
                            }
                        });
                    }

                    List<PlayedWordInfo> player1WordsPlayed = new List<PlayedWordInfo>();
                    using (SqlCommand command = new SqlCommand(
                        "Select Word, Score from Words where GameID = @gameId and Player = @player", conn, trans))
                    {
                        command.Parameters.AddWithValue("@gameId", gameId);
                        command.Parameters.AddWithValue("@player", player1Id);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                player1WordsPlayed.Add(new PlayedWordInfo()
                                {
                                    Word = reader.GetString(0),
                                    Score = reader.GetInt32(1)
                                });
                            }
                        }
                    }

                    List<PlayedWordInfo> player2WordsPlayed = new List<PlayedWordInfo>();
                    using (SqlCommand command = new SqlCommand(
                        "Select Word, Score from Words where GameID = @gameId and Player = @player", conn, trans))
                    {
                        command.Parameters.AddWithValue("@gameId", gameId);
                        command.Parameters.AddWithValue("@player", player2Id);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                player2WordsPlayed.Add(new PlayedWordInfo()
                                {
                                    Word = reader.GetString(0),
                                    Score = reader.GetInt32(1)
                                });
                            }
                        }
                    }

                    trans.Commit();
                    return Tuple.Create(OK, new Game
                    {
                        GameState = gameState,
                        Board = board,
                        TimeLimit = timeLimit,
                        TimeLeft = timeLeft,
                        Player1 = new Player()
                        {
                            Nickname = player1Nickname,
                            Score = player1Score,
                            WordsPlayed = player1WordsPlayed
                        },
                        Player2 = new Player()
                        {
                            Nickname = player2Nickname,
                            Score = player2Score,
                            WordsPlayed = player2WordsPlayed
                        }
                    });
                }
            }
        }

        /// <summary>
        /// Returns all legal words in the pending game's gameboard. 
        /// </summary>
        public static HashSet<string> GetAllLegalWordsForPendingGame(string ID)
        {
            using (SqlConnection conn = new SqlConnection(BoggleDbConnString))
            {
                conn.Open();
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    using (SqlCommand command = new SqlCommand(
                        "Select Board from Games where Player1 = @ID", conn, trans))
                    {
                        command.Parameters.AddWithValue("@ID", ID);
                        var boardStr = (string)command.ExecuteScalar();
                        var boggleBoard = new BoggleBoard(boardStr);
                        return new HashSet<string>(dictionary.Where(boggleBoard.CanBeFormed));
                    }
                }
            }
        }

        /// <summary>
        /// Reads in the game's dictionary. Returns it as a set. 
        /// </summary>
        /// <returns></returns>
        private static HashSet<string> ReadInDictionary()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\dictionary.txt");
            return new HashSet<string>(File.ReadAllLines(path).Select(word => word.ToUpper()));
        }
    }
}
