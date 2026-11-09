namespace Hrms.Api.Common;

public sealed record ApiResponse<T>(T Data, PageMeta? Meta = null);

public sealed record PageMeta(int Page, int PageSize, int Total);
