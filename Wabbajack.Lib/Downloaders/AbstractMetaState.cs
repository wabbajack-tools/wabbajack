namespace Wabbajack.Lib.Downloaders
{
    public interface IAbstractMetaState
    {
        string URL { get; set; }
        string Name { get; set; }
        string Author { get; set; }
        string Version { get;set; }
        string ImageURL { get; set; }
        bool IsNSFW { get; set; }
        string Description { get; set; }
    }
}
