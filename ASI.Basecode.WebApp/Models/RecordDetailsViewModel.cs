using System.Collections.Generic;

namespace ASI.Basecode.WebApp.Models;

public sealed record RecordDetailsViewModel(string Id, string Title, IReadOnlyDictionary<string, string> Fields, string ImageUrl = null);
