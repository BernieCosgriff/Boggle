using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Boggle
{

    /// <summary>
    /// Represents of a game of boggle. 
    /// </summary>
    [DataContract]
    public class Game
    {
        /// <summary>
        /// May be "active", "pending", or "completed". 
        /// </summary>
        [DataMember(EmitDefaultValue = false)]
        public string GameState { get; set; }

        /// <summary>
        /// The board this game is played on.
        /// </summary>
        [DataMember(EmitDefaultValue = false)]
        public string Board { get; set; }

        /// <summary>
        /// The first player in the game.
        /// </summary>
        [DataMember(EmitDefaultValue = false)]
        public Player Player1 { get; set; }

        /// <summary>
        /// The second player in the game.
        /// </summary>
        [DataMember(EmitDefaultValue = false)]
        public Player Player2 { get; set; }

        /// <summary>
        /// The timelimit agreed upon by the players (or set by one player, if a second
        /// player has not joined yet).
        /// </summary>
        [DataMember(EmitDefaultValue = false)]
        public int? TimeLimit { get; set; }

        /// <summary>
        /// The time remaining in the game, in seconds. Note that this will be -1 if
        /// the game has not began yet. 
        /// </summary>
        [DataMember(EmitDefaultValue = false)]
        public long? TimeLeft { get; set; }
    }

    /// <summary>
    /// Represents a player in the game.
    /// </summary>
    [DataContract]
    public class Player
    {
        /// <summary>
        /// The nickname of this player.
        /// </summary>
        [DataMember(EmitDefaultValue = false)]
        public string Nickname { get; set; }

        /// <summary>
        /// The score held by this player in whatever game he or she is playing. 
        /// </summary>
        [DataMember(EmitDefaultValue = false)]
        public int? Score { get; set; }

        /// <summary>
        /// A list of the words played by this player mapped to the scores associated with those words.
        /// </summary>
        [DataMember(EmitDefaultValue = false)]
        public List<PlayedWordInfo> WordsPlayed { get; set; }
    }

    /// <summary>
    /// A wrapper for a user nickname. 
    /// </summary>
    public class UserInfo
    {
        public string Nickname { get; set; }
    }

    /// <summary>
    /// A wrapper for a user token. 
    /// </summary>
    public class Token
    {
        public string UserToken { get; set; }
    }

    /// <summary>
    /// A wrapper for a GameID. 
    /// </summary>
    public class GameIdInfo
    {
        public int GameID { get; set; }
    }

    /// <summary>
    /// Information about a word that has been played by a user. 
    /// </summary>
    public class PlayedWordInfo
    {
        public string Word { get; set; }
        public int Score { get; set; }
    }

    /// <summary>
    /// A container corresponding to a score for a word played by a user.
    /// </summary>
    public class PlayedWordScore
    {
        public int Score { get; set; }
    }

    /// <summary>
    /// The information necessary to join a game. 
    /// </summary>
    public class JoinGameInfo
    {
        public string UserToken { get; set; }

        public int TimeLimit { get; set; }
    }

    /// <summary>
    /// The information necessary to cancel a game. 
    /// </summary>
    public class CancelInfo
    {
        public string UserToken { get; set; }
    }

    /// <summary>
    /// Represents a played word. 
    /// </summary>
    public class WordToPlayInfo
    {
        public string Word { get; set; }
        public string UserToken { get; set; }
    }
 
}