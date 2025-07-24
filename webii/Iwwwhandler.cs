namespace webii
{
    internal interface Iwwwhandler
    {
        public void Start();
        public void Stop();
        public void Set404Page(string path);
    }
}