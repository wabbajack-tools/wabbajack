namespace Wabbajack.Lib.Downloaders
{
    public abstract class AbstractMetaState
    {
        public abstract string URL { get; set; }
        public abstract string Name { get; set; }
        public abstract string Author { get; set; }
        public abstract string Version { get;set; }
        public abstract string ImageURL { get; set; }
        public abstract bool IsNSFW { get; set; }
        public abstract string Description { get; set; }
    }
}
