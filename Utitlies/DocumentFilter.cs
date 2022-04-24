using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;

using Swashbuckle.AspNetCore.SwaggerGen;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RESTfulAPI.Utitlies
{
	public class DocumentFilter : IDocumentFilter
	{
		private readonly IHttpContextAccessor _httpContextAccessor;

		public DocumentFilter(IHttpContextAccessor httpContextAccessor)
		{
			_httpContextAccessor = httpContextAccessor;
		}

		public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
		{
			string url;

			HttpRequest request = _httpContextAccessor.HttpContext?.Request;
			if (request != null)
			{
				url = $"{request.Scheme}://{request.Host}";
			}
			else
			{
				url = "https://localhost";

				// we need to modify the Key, but that is read-only so let's just make a copy of the Paths property
				OpenApiPaths copy = new();
				foreach (KeyValuePair<string, OpenApiPathItem> path in swaggerDoc.Paths)
				{
					string newKey = Regex.Replace(path.Key, "/api/v[^/]*", string.Empty);
					copy.Add(newKey, path.Value);
				}
				swaggerDoc.Paths.Clear();
				swaggerDoc.Paths = copy;
			}

			swaggerDoc.Servers.Add(new OpenApiServer { Url = url });
		}
	}
}
