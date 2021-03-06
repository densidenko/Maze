using System;
using System.IO;
using System.Text;
using Maze.Modules.Api.Response;

namespace Maze.Modules.Api.Formatters
{
    /// <summary>
    ///     A context object for <see cref="IOutputFormatter.WriteAsync(OutputFormatterWriteContext)" />.
    /// </summary>
    public class OutputFormatterWriteContext : OutputFormatterCanWriteContext
    {
        /// <summary>
        ///     Creates a new <see cref="OutputFormatterWriteContext" />.
        /// </summary>
        /// <param name="context">The <see cref="MazeContext" /> for the current request.</param>
        /// <param name="writerFactory">The delegate used to create a <see cref="TextWriter" /> for writing the response.</param>
        /// <param name="objectType">The <see cref="Type" /> of the object to write to the response.</param>
        /// <param name="object">The object to write to the response.</param>
        public OutputFormatterWriteContext(MazeContext context, Func<Stream, Encoding, TextWriter> writerFactory,
            Type objectType, object @object)
            : base(context)
        {
            WriterFactory = writerFactory ?? throw new ArgumentNullException(nameof(writerFactory));
            ObjectType = objectType;
            Object = @object;
        }

        /// <summary>
        ///     <para>
        ///         Gets or sets a delegate used to create a <see cref="TextWriter" /> for writing text to the response.
        ///     </para>
        ///     <para>
        ///         Write to <see cref="MazeResponse.Body" /> directly to write binary data to the response.
        ///     </para>
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         The <see cref="TextWriter" /> created by this delegate will encode text and write to the
        ///         <see cref="MazeResponse.Body" /> stream. Call this delegate to create a <see cref="TextWriter" />
        ///         for writing text output to the response stream.
        ///     </para>
        ///     <para>
        ///         To implement a formatter that writes binary data to the response stream, do not use the
        ///         <see cref="WriterFactory" /> delegate, and use <see cref="MazeResponse.Body" /> instead.
        ///     </para>
        /// </remarks>
        public virtual Func<Stream, Encoding, TextWriter> WriterFactory { get; protected set; }
    }
}