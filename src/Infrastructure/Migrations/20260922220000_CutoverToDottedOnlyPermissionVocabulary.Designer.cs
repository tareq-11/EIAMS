using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260922220000_CutoverToDottedOnlyPermissionVocabulary")]
partial class CutoverToDottedOnlyPermissionVocabulary
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        // 2A is a data-only vocabulary cutover; the relational model is unchanged.
    }
}
