using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;

namespace ASI.Basecode.Services.ServiceModels
{
    /// <summary>
    /// The editable values shown when an operator opens a member form.
    /// This is evidence of state, not an authorization credential.
    /// </summary>
    public sealed record MemberEditSnapshot(
        int MemberId,
        int UserId,
        string FullName,
        string StudentNumber,
        string Course,
        int YearLevel,
        string Section,
        string MobileNumber,
        string MembershipType)
    {
        public static MemberEditSnapshot FromModel(MemberViewModel model) => new(
            model.MemberId,
            model.UserId,
            ProfileOptions.NormalizeName(model.FullName),
            model.StudentNumber?.Trim(),
            ProfileOptions.NormalizeCourse(model.Course),
            model.YearLevel,
            ProfileOptions.NormalizeSection(model.Section),
            NormalizeOptional(model.MobileNumber),
            NormalizeOptional(model.MembershipType));

        public static MemberEditSnapshot FromEntities(Member member, string fullName) => new(
            member.MemberId,
            member.UserId,
            ProfileOptions.NormalizeName(fullName),
            member.StudentNumber?.Trim(),
            ProfileOptions.NormalizeCourse(member.Course),
            member.YearLevel,
            ProfileOptions.NormalizeSection(member.Section),
            NormalizeOptional(member.MobileNumber),
            NormalizeOptional(member.MembershipType));

        private static string NormalizeOptional(string value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
