namespace Imazen.Abstractions.SelfTestable
{
    
    internal interface ISelfTestable
    {
        /// <summary>
        /// Should run self tests for the components and any sub-components it owns.
        /// </summary>
        /// <returns></returns>
        IList<Task<ISelfTestResult>> RunSelfTests(CancellationToken cancellationToken = default);
    }
}