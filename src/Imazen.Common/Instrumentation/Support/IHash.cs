namespace Imazen.Common.Instrumentation.Support
{
    interface IHash
    {
        uint ComputeHash(uint value);
        IHash GetNext();
    }
}
