using Presenter.Core.Abstractions;
using Presenter.Engine.Com;

namespace Presenter.App_Code
{
    /// <summary>WPF image loading for engines that cannot reference WPF themselves.</summary>
    public class WpfSlideImageLoader : ISlideImageLoader
    {
        public object Load(string filename, int maxWidth, int maxHeight) =>
            ComPresentationEngine.RetrieveImage(filename, maxWidth, maxHeight);
    }
}
