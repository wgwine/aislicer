namespace AIAudioSlicer
{
    class Program
    {
        static void Main(string[] args)
        {
            AudioUtility au = new AudioUtility();
            au.go(args).Wait();
        }
    }
}
