﻿using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Sdl.Web.GraphQLClient.Request;
using Sdl.Web.HttpClient.Utils;
using Sdl.Web.PublicContentApi.ContentModel;
using Sdl.Web.PublicContentApi.Utils;

namespace Sdl.Web.PublicContentApi
{
    /// <summary>
    /// QueryBuilder
    /// </summary>
    public class QueryBuilder
    {
        private StringBuilder _query;
        private Dictionary<string, object> _variables;
        private List<JsonConverter> _convertors;
        private IContextData _contextData;
        private string _customMetaFilter;
        private string _operationName;
        private int _descendantLevels;
        private bool _renderContent;

        public QueryBuilder()
        {
            _query = new StringBuilder();
        }

        public QueryBuilder(string query)
        {
            _query = new StringBuilder(query);
        }

        public QueryBuilder WithQuery(string query)
        {
            _query.Append(query);
            return this;
        }

        public QueryBuilder WithQueryResource(string queryResourceName, bool loadIncludedFragments)
        {
            _query.Append(Queries.Load(queryResourceName, loadIncludedFragments));
            return this;
        }

        public QueryBuilder WithCustomMetaFilter(string customMetaFilter)
        {
            _customMetaFilter = customMetaFilter;
            return this;
        }

        public QueryBuilder WithOperationName(string operationName)
        {
            _operationName = operationName;
            return this;
        }

        public QueryBuilder WithConvertor(JsonConverter convertor)
        {
            if(_convertors == null) _convertors = new List<JsonConverter>();
            _convertors.Add(convertor);
            return this;
        }

        public QueryBuilder WithPublicationId(int publicationId) => WithVariable("publicationId", publicationId);

        public QueryBuilder WithPageId(int pageId) => WithVariable("pageId", pageId);

        public QueryBuilder WithBinaryId(int binaryId) => WithVariable("binaryId", binaryId);

        public QueryBuilder WithUrl(string url) => WithVariable("url", UrlEncoding.UrlEncodeNonAscii(url));

        public QueryBuilder WithCmUri(CmUri cmUri)
            => WithVariable("cmUri", cmUri).WithNamespace(cmUri.Namespace).WithPublicationId(cmUri.PublicationId);

        public QueryBuilder WithNamespace(ContentNamespace ns) => WithVariable("namespaceId", ns);

        public QueryBuilder WithInputPublicationFilter(InputPublicationFilter filter)
            => WithVariable("inputPublicationFilter", filter);

        public QueryBuilder WithInputItemFilter(InputItemFilter filter) => WithVariable("inputItemFilter", filter);

        public QueryBuilder WithInputSortParam(InputSortParam sort) => WithVariable("inputSortParam", sort);

        public QueryBuilder WithPagination(IPagination pagination) => WithVariable("first", pagination.First).WithVariable("after", pagination.After);

        public QueryBuilder WithRenderRelativeLink(bool renderRelativeLink)
            => WithVariable("renderRelativeLink", renderRelativeLink);

        public QueryBuilder WithRenderContent(bool renderContent)
        {
            _renderContent = renderContent;
            return this;
        }       

        public QueryBuilder WithContextClaim(ClaimValue claim)
        {
            if (_contextData == null) _contextData = new ContextData();
            _contextData.ClaimValues.Add(claim);
            return this;
        }

        public QueryBuilder WithContextData(IContextData contextData)
        {
            _contextData = MergeContextData(_contextData, contextData);
            return this;
        }

        public QueryBuilder WithVariable(string name, object value)
        {
            if (_variables == null) _variables = new Dictionary<string, object>();
            _variables.Add(name, value);
            return this;
        }

        public QueryBuilder WithDescendantLevels(int descendantLevels)
        {
            _descendantLevels = descendantLevels;
            return this;
        }

        public QueryBuilder ReplaceTag(string tagName, string value)
        {
            _query.Replace($"@{tagName}", value);
            return this;
        }

        public QueryBuilder LoadFragments()
        {
            _query = new StringBuilder(Queries.LoadFragments(_query.ToString()));
            return this;
        }

        public QueryBuilder WithIncludeRegion(string regionName, bool include)
        {
            _query =
                new StringBuilder(QueryHelpers.ParseIncludeRegions(_query.ToString(), regionName, include));
            return this;
        }

        public IGraphQLRequest Build()
        {
            // Replace tags in graphQL query.
            ReplaceTag("customMetaArgs",
                string.IsNullOrEmpty(_customMetaFilter) ? "" : $"(filter: \"{_customMetaFilter}\")");

            ReplaceTag("renderContentArgs", 
                $"(renderContent: {(_renderContent ? "true" : "false")})");

            if (_variables != null)
            {
                ReplaceTag("variantsArgs", _variables.ContainsKey("url") ? $"(url: \"{_variables["url"]}\")" : "");
            }

            string query = _query.ToString();
            QueryHelpers.ExpandRecursiveFragment(ref query, null, _descendantLevels);
            
            if (_contextData != null)
            {
                WithVariable("contextData", _contextData.ClaimValues);
            }

            return new GraphQLRequest
            {
                OperationName = _operationName,
                Query = query,
                Variables = _variables,
                Convertors = _convertors
            };
        }      

        private static IContextData MergeContextData(IContextData localContextData, IContextData globalContextData)
        {
            if (localContextData == null && globalContextData == null)
                return new ContextData();

            if (localContextData == null)
                return globalContextData;

            if (globalContextData == null)
                return localContextData;

            IContextData merged = new ContextData();
            merged.ClaimValues = globalContextData.ClaimValues.Concat(localContextData.ClaimValues).ToList();
            return merged;
        }
    }
}
