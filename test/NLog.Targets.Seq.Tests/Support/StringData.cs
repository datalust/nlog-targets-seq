namespace NLog.Targets.Seq.Tests.Support
{
    class StringData
    {
        public string Data { get; set; }

        public override string ToString()
        {
            return "SD:" + Data;
        }
    }
}
