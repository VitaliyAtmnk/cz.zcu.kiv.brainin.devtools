using System.Collections.Generic;

namespace BrainIn.DevTools.Editor.Diagnostics.Results
{
    /// <summary>
    /// Summarizes one round from a BrainIn result JSON.
    /// </summary>
    public sealed class ResultRoundDiagnostics
    {
        /// <summary>
        /// Gets or sets the one-based round number.
        /// </summary>
        public int RoundNumber { get; set; }

        /// <summary>
        /// Gets or sets BrainIn roundTime value.
        /// </summary>
        public string RoundTime { get; set; }

        /// <summary>
        /// Gets or sets BrainIn playingTime value.
        /// </summary>
        public string PlayingTime { get; set; }

        /// <summary>
        /// Gets or sets BrainIn finished value.
        /// </summary>
        public string Finished { get; set; }

        /// <summary>
        /// Gets or sets BrainIn successfully value.
        /// </summary>
        public string Successfully { get; set; }

        /// <summary>
        /// Gets or sets BrainIn finalClickId value.
        /// </summary>
        public string FinalClickId { get; set; }

        /// <summary>
        /// Gets or sets selectedAnswer value from customData.
        /// </summary>
        public string SelectedAnswer { get; set; }

        /// <summary>
        /// Gets or sets selectedAnswerActionId value from customData.
        /// </summary>
        public string SelectedAnswerActionId { get; set; }

        /// <summary>
        /// Gets or sets correctAnswer value from customData.
        /// </summary>
        public string CorrectAnswer { get; set; }

        /// <summary>
        /// Gets or sets timedOut value from customData.
        /// </summary>
        public string TimedOut { get; set; }

        /// <summary>
        /// Gets or sets reactionTimeSeconds value from customData.
        /// </summary>
        public string ReactionTimeSeconds { get; set; }

        /// <summary>
        /// Gets custom data key-value pairs parsed from BrainIn customData.
        /// </summary>
        public Dictionary<string, string> CustomData { get; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets or sets number of mouse clicks recorded in the round.
        /// </summary>
        public int MouseClickCount { get; set; }

        /// <summary>
        /// Gets or sets number of keystrokes recorded in the round.
        /// </summary>
        public int KeystrokeCount { get; set; }
    }
}