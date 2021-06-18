using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Extensions.AutoLinks;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Wabbajack.Web.Shared
{
    public class MarkdownComponent : ComponentBase
    {
        [Inject]
        public HttpClient HttpClient { get; set; }
        
        [Parameter]
        public string MarkdownUrl { get; set; }
        
        private string MarkdownContent { get; set; }
        
        protected override async Task OnParametersSetAsync()
        {
            await base.OnParametersSetAsync();
            
            if (MarkdownUrl != null)
                MarkdownContent = await HttpClient.GetStringAsync(MarkdownUrl, CancellationToken.None);
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            base.BuildRenderTree(builder);
            if (MarkdownContent == null) return;

            var pipeline = new MarkdownPipelineBuilder()
                .Use<MarkdownAnchorExtension>()
                .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
                .UseAutoLinks(new AutoLinkOptions
                {
                    OpenInNewWindow = true,
                    UseHttpsForWWWLinks = true
                })
                .Build();
            
            // TODO: maybe add https://github.com/mganss/HtmlSanitizer
            var markupString = new MarkupString(Markdown.ToHtml(MarkdownContent, pipeline));
            builder.AddContent(0, markupString);
        }
    }

    public class MarkdownAnchorExtension : IMarkdownExtension
    {
        public void Setup(MarkdownPipelineBuilder pipeline)
        {
            // Make sure we don't have a delegate twice
            pipeline.DocumentProcessed -= PipelineOnDocumentProcessed;
            pipeline.DocumentProcessed += PipelineOnDocumentProcessed;
        }

        public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
        {
        }

        private static void PipelineOnDocumentProcessed(MarkdownDocument document)
        {
            foreach (var node in document.Descendants())
            {
                if (node is not LinkInline link) continue;
                if (link.IsImage) continue;
                if (link.IsShortcut) continue;

                var url = link.Url;
                if (url == null) continue;
                if (url.Length < 2) continue;
                if (url[0] != '#') continue;
                
                // TODO:
                // https://github.com/dotnet/aspnetcore/issues/8393#issuecomment-559294599
                link.GetAttributes().AddPropertyIfNotExist("target", "_top");
            }
        }
    }
}
