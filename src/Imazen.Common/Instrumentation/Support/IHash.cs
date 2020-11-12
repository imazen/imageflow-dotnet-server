namespace Imazen.Common.Instrumentation.Support
{
    internal interface IHash
    {
        uint ComputeHash(uint value);
        IHash GetNext();
    }
}
