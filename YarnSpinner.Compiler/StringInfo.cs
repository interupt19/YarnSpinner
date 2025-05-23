// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

// Uncomment to ensure that all expressions have a known type at compile time
// #define VALIDATE_ALL_EXPRESSIONS

namespace Yarn.Compiler
{
    /// <summary>
    /// Information about a string. Stored inside a string table, which is
    /// produced from the Compiler.
    /// </summary>
    /// <remarks>
    /// You do not create instances of this class yourself. They are
    /// generated by the <see cref="Compiler"/>.
    /// </remarks>
    public struct StringInfo
    {
        /// <summary>
        /// The original text of the string.
        /// </summary>
        /// <remarks>
        /// This field is <see langword="null"/> if <see cref="shadowLineID"/>
        /// is not null.
        /// </remarks>
        public string? text;

        /// <summary>
        /// The name of the node that this string was found in.
        /// </summary>
        public string nodeName;

        /// <summary>
        /// The line number at which this string was found in the file.
        /// </summary>
        public int lineNumber;

        /// <summary>
        /// The name of the file this string was found in.
        /// </summary>
        public string fileName;

        /// <summary>
        /// Indicates whether this string's line ID was implicitly
        /// generated.
        /// </summary>
        /// <remarks>
        /// Implicitly generated line IDs are not guaranteed to remain the
        /// same across multiple compilations. To ensure that a line ID
        /// remains the same, you must define it by adding a line tag to the
        /// line.
        /// </remarks>
        public bool isImplicitTag;

        /// <summary>
        /// The metadata (i.e. hashtags) associated with this string.
        /// </summary>
        /// <remarks>
        /// This array will contain any hashtags associated with this
        /// string besides the <c>#line:</c> hashtag.
        /// </remarks>
        public string[] metadata;

        /// <summary>
        /// The ID of the line that this line is shadowing, or null if this line
        /// is not shadowing another line.
        /// </summary>
        public string? shadowLineID;

        /// <summary>
        /// Initializes a new instance of the <see cref="StringInfo"/> struct.
        /// </summary>
        /// <param name="text">The text of the string.</param>
        /// <param name="fileName">The file name.</param>
        /// <param name="nodeName">The node name.</param>
        /// <param name="lineNumber">The line number.</param>
        /// <param name="isImplicitTag">If <c>true</c>, this string info is
        /// stored with an implicit line ID.</param>
        /// <param name="metadata">The string's metadata.</param>
        /// <param name="shadowID">The ID of the line that this entry is
        /// shadowing, or null.</param>
        internal StringInfo(string? text, string fileName, string nodeName, int lineNumber, bool isImplicitTag, string[] metadata, string? shadowID)
        {
            this.text = text;
            this.nodeName = nodeName;
            this.lineNumber = lineNumber;
            this.fileName = fileName;
            this.isImplicitTag = isImplicitTag;
            this.shadowLineID = shadowID;

            if (metadata != null)
            {
                this.metadata = metadata;
            }
            else
            {
                this.metadata = new string[] { };
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{this.text} ({this.fileName}:{this.lineNumber})";
        }
    }
}
