namespace Maze.Modules.Api.Response
{
    /// <summary>
    ///     Represents an <see cref="ActionResult" /> that when executed will
    ///     do nothing.
    /// </summary>
    public class EmptyResult : ActionResult
    {
        /// <inheritdoc />
        public override void ExecuteResult(ActionContext context)
        {
        }
    }
}