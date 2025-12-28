namespace StreamSync.DTOs
{
    public class PaginationQueryDto
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? Search { get; set; }
        public string? SortBy { get; set; } = "CreatedAt";
        public string? SortOrder { get; set; } = "desc";

        // Validation
        public void Validate()
        {
            if (Page < 1) Page = 1;
            if (PageSize < 1) PageSize = 10;
            if (PageSize > 50) PageSize = 50; // Limit max page size
            
            // Validate sort order
            if (SortOrder?.ToLower() != "asc" && SortOrder?.ToLower() != "desc")
                SortOrder = "desc";
        }
    }
}