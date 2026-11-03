using System.Data;
using System.Xml.Linq;
using Chloe.Annotations;
using Xunit;

namespace Chloe.SonnetDB.Tests;

public class ReleaseCommentTests : TestDatabase
{
    [Fact]
    public void Xml_column_comments_preserve_quotes_and_physical_table_name()
    {
        var assembly = typeof(CommentedMappingRow).Assembly;
        string path = Path.ChangeExtension(assembly.Location, ".xml");
        byte[]? original = File.Exists(path) ? File.ReadAllBytes(path) : null;
        try
        {
            new XDocument(new XElement("doc", new XElement("members", new XElement("member",
                new XAttribute("name", "P:" + typeof(CommentedMappingRow).FullName + ".Value"),
                new XElement("summary", "设备 O'Brien"))))).Save(path);
            CreateTable<CommentedMappingRow>("CommentedPhysical");
            using var connection = ConnectionFactory.CreateConnection();
            connection.Open();
            var metadata = ((System.Data.Common.DbConnection)connection).GetSchema("Columns");
            Assert.Contains(metadata.Rows.Cast<DataRow>(), row => row.ItemArray.Contains("CommentedPhysical") && row.ItemArray.Contains("设备 O'Brien"));
        }
        finally
        {
            if (original is null) File.Delete(path);
            else File.WriteAllBytes(path, original);
        }
    }
}

[Table("CommentedLogical")]
public class CommentedMappingRow
{
    [Column(IsPrimaryKey = true), NonAutoIncrement] public int Id { get; set; }
    public string? Value { get; set; }
}
