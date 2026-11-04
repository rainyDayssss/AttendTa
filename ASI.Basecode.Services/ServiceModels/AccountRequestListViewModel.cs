using System;
using System.Collections.Generic;

namespace ASI.Basecode.Services.ServiceModels
{
    public class AccountRequestListItemViewModel
    {
        public int RequestId { get; set; }
        public string StudentNumber { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Course { get; set; }
        public int YearLevel { get; set; }
        public string Section { get; set; }
        public string MobileNumber { get; set; }
        public DateTime RequestedAt { get; set; }
    }

    public class AccountRequestListViewModel
    {
        public IReadOnlyList<AccountRequestListItemViewModel> Items { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public string Search { get; set; }
    }
}
