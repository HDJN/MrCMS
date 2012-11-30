using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using System.Xml;
using MrCMS.Entities.Documents;
using MrCMS.Entities.Documents.Layout;
using MrCMS.Entities.Documents.Media;
using MrCMS.Entities.Documents.Web;
using MrCMS.Entities.People;
using MrCMS.Helpers;
using MrCMS.Models;

namespace MrCMS.Services
{
    public class NavigationService : INavigationService
    {
        private readonly IDocumentService _documentService;
        private readonly IUserService _userService;

        public NavigationService(IDocumentService documentService, IUserService userService)
        {
            _documentService = documentService;
            _userService = userService;
        }

        public SiteTree<Webpage> GetWebsiteTree(int? depth = null)
        {
            var tree = new SiteTree<Webpage>
                           {

                               Id = null,
                               ParentId = null,
                               IconClass = "icon-asterisk",
                               Name = "Root",
                               NodeType = "Webpage",
                               CanAddChild = DocumentTypeHelper.GetValidWebpageDocumentTypes(null).Any()
                           };
            tree.Children = GetNodes(tree, _documentService.GetAdminDocumentsByParentId<Webpage>(null), null, maxDepth: depth);

            return tree;
        }

        public SiteTree<MediaCategory> GetMediaTree()
        {
            var tree = new SiteTree<MediaCategory>
                           {
                               Id = null,
                               ParentId = null,
                               IconClass = "icon-asterisk",
                               Name = "Root",
                               NodeType = "MediaCategory",
                               CanAddChild = true
                           };
            tree.Children = GetNodes(tree, _documentService.GetAdminDocumentsByParentId<MediaCategory>(null), null);

            return tree;
        }

        public SiteTree<Layout> GetLayoutList()
        {
            var tree = new SiteTree<Layout>
                           {
                               Id = null,
                               ParentId = null,
                               IconClass = "icon-asterisk",
                               Name = "Root",
                               NodeType = "Layout",
                               CanAddChild = true
                           };

            tree.Children = GetNodes(tree, _documentService.GetAdminDocumentsByParentId<Layout>(null), null);
            return tree;
        }

        public SiteTree<User> GetUserList()
        {
            var tree = new SiteTree<User>
                           {
                               Children = GetUserNodes(_userService.GetAllUsers(), null),
                               Id = null,
                               ParentId = null,
                               IconClass = "icon-asterisk",
                               Name = "Root",
                               NodeType = "Root"
                           };

            return tree;
        }

        public GetMoreResult GetMore(int parentId, int previousId)
        {
            throw new System.NotImplementedException();
        }

        public IEnumerable<SelectListItem> GetParentsList()
        {
            var selectListItems = GetPageListItems(GetWebsiteTree().Children, 1).ToList();
            selectListItems.Insert(0, new SelectListItem {Selected = false, Text = "Root", Value = ""});
            return selectListItems;
        }

        public string GetSiteMap(UrlHelper urlHelper)
        {
            var websiteTree = GetWebsiteTree();

            var xmlDocument = new XmlDocument();
            var xmlDeclaration = xmlDocument.CreateXmlDeclaration("1.0", "utf-8", null);
            xmlDocument.InsertBefore(xmlDeclaration, xmlDocument.DocumentElement);

            var urlset =
                xmlDocument.AppendChild(xmlDocument.CreateElement("urlset"));


            var standardNs = xmlDocument.CreateAttribute("xmlns");
            standardNs.Value = "http://www.google.com/schemas/sitemap/0.9";
            var imageNs = xmlDocument.CreateAttribute("xmlns:image");
            imageNs.Value = "http://www.google.com/schemas/sitemap-image/1.1";
            urlset.Attributes.Append(imageNs);

            var children = websiteTree.Children;

            while (children.Any())
            {
                children.ForEach(node =>
                                     {
                                         if (node.Item.Published)
                                         {
                                             var url =
                                                 urlset.AppendChild(xmlDocument.CreateNode(XmlNodeType.Element, "url",
                                                                                           ""));

                                             var loc =
                                                 url.AppendChild(xmlDocument.CreateNode(XmlNodeType.Element, "loc", ""));
                                             loc.InnerText = urlHelper.AbsoluteContent(node.Item.LiveUrlSegment);
                                             if (node.Item is TextPage &&
                                                 !string.IsNullOrEmpty((node.Item as TextPage).FeatureImage))
                                             {
                                                 var image = url.AppendChild(xmlDocument.CreateElement("image:image"));
                                                 var imageLoc = image.AppendChild(xmlDocument.CreateElement("image:loc"));
                                                 imageLoc.InnerText = urlHelper.AbsoluteContent((node.Item as TextPage).FeatureImage);
                                             }
                                             var lastMod =
                                                 url.AppendChild(xmlDocument.CreateNode(XmlNodeType.Element, "lastmod",
                                                                                        ""));

                                             lastMod.InnerText = node.Item.UpdatedOn.ToString("yyyy-MM-dd");

                                         }
                                     });

                children = children.SelectMany(node => node.Children).ToList();
            }
            StringBuilder sb = new StringBuilder();
            TextWriter tw = new StringWriter(sb);
            xmlDocument.WriteTo(new XmlTextWriter(tw));

            var content = sb.ToString();
            return content;
        }

        public IEnumerable<SelectListItem> GetDocumentTypes(string type)
        {
            return DocumentTypeHelper.DocumentTypeDefinitions
                                   .BuildSelectItemList(definition => definition.Name, definition => definition.TypeName,
                                                        definition => definition.TypeName == type, "Select type");
        }

        private IEnumerable<SelectListItem> GetPageListItems(IEnumerable<SiteTreeNode<Webpage>> nodes, int depth)
        {
            var items = new List<SelectListItem>();

            foreach (var node in nodes.Where(node => node.Children.Any()))
            {
                items.Add(new SelectListItem { Selected = false, Text = GetDashes(depth) + node.Name, Value = node.Id.ToString() });
                items.AddRange(GetPageListItems(node.Children, depth + 1));
            }

            return items;
        }

        private string GetDashes(int depth)
        {
            return string.Empty.PadRight(depth * 2, '-');
        }

        private List<SiteTreeNode<User>> GetUserNodes(IEnumerable<User> users, int? rootId)
        {
            var list = new List<SiteTreeNode<User>>();
            users.ForEach(user => list.Add(new SiteTreeNode<User>
                                               {
                                                   Children = new List<SiteTreeNode<User>>(),
                                                   IconClass = "icon-user",
                                                   Id = user.Id,
                                                   ParentId = rootId,
                                                   Name = user.Name,
                                                   NodeType = "User",
                                                   Item = user
                                               }));
            return list;
        }


        private List<SiteTreeNode<T>> GetNodes<T>(SiteTreeNode parent, IEnumerable<T> nodes, int? parentId, int? maxChildren = null, int? maxDepth = null) where T : Document
        {
            var list = new List<SiteTreeNode<T>>();

            if (maxChildren.HasValue)
            {
                nodes = nodes.Take(maxChildren.Value);
            }
            nodes.ForEach(document =>
                              {
                                  var count = _documentService.GetAdminDocumentsByParentId<T>(document.Id).Count();
                                  var siteTreeNode = new SiteTreeNode<T>
                                                         {
                                                             Total =
                                                                 count,
                                                             IconClass = DocumentTypeHelper.GetIconClass(document),
                                                             Id = document.Id,
                                                             ParentId = parentId,
                                                             Name = document.Name,
                                                             NodeType = GetNodeType(document),
                                                             CustomSort = IsCustomSort(document.Id),
                                                             CanAddChild =
                                                                 !(document is Webpage) ||
                                                                 (document as Webpage).GetValidWebpageDocumentTypes().
                                                                     Any(),
                                                             IsPublished =
                                                                 !(document is Webpage) ||
                                                                 (document as Webpage).Published,
                                                             RevealInNavigation =
                                                                 (document is Webpage) &&
                                                                 (document as Webpage).RevealInNavigation,
                                                             Item = document
                                                         };
                                  siteTreeNode.Children =
                                      maxDepth.HasValue
                                          ? maxDepth.Value == 0
                                                ? new List<SiteTreeNode<T>>()
                                                : GetNodes(siteTreeNode,
                                                    _documentService.GetAdminDocumentsByParentId
                                                        <T>(document.Id), document.Id,
                                                    document.GetMaxChildNodes(), maxDepth - 1)
                                          : GetNodes(siteTreeNode,
                                              _documentService.GetAdminDocumentsByParentId<T>(
                                                  document.Id), document.Id,
                                              document.GetMaxChildNodes());
                                  siteTreeNode.Parent = parent;
                                  list.Add(siteTreeNode);
                              });
            return list;
        }

        private bool IsCustomSort(int? documentId)
        {
            if (documentId.HasValue)
            {
                var document = _documentService.GetDocument<Document>(documentId.Value);
                var documentTypeDefinition = DocumentTypeHelper.GetDefinitionByType(document.GetType());
                if (documentTypeDefinition != null)
                    return documentTypeDefinition.CustomSort;
            }
            return false;
        }

        private string GetNodeType(Document document)
        {
            return document is Webpage
                       ? "Webpage"
                       : document is Layout
                             ? "Layout"
                             : document is MediaCategory
                                   ? "MediaCategory"
                                   : "";

        }
    }
}