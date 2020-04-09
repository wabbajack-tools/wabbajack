using Xunit;

namespace Wabbajack.Common.Test
{
    public class IniTests
    {
        
        [Fact]
        public void TestByteArrayParsing()
        {
            Assert.Equal("bar", @"[General]
                                     foo = bar".LoadIniString().General.foo);

            Assert.Equal("baz\\bar", @"[General]
                                     foo = baz\\bar".LoadIniString().General.foo);
            
            Assert.Equal("bar", @"[General]
                                     foo = @ByteArray(bar)".LoadIniString().General.foo);

            Assert.Equal("foo\\h̴̹͚̎é̶̘͙̐l̶͕̔͑p̴̯̋͂m̶̞̮͘͠e̸͉͙͆̄\\baz", @"[General]
                                     foo = @ByteArray(foo\\\x68\xcc\xb4\xcc\x8e\xcc\xb9\xcd\x9a\x65\xcc\xb6\xcd\x81\xcc\x90\xcc\x98\xcd\x99\x6c\xcc\xb6\xcc\x94\xcd\x91\xcd\x95\x70\xcc\xb4\xcc\x8b\xcd\x82\xcc\xaf\x6d\xcc\xb6\xcd\x98\xcd\xa0\xcc\x9e\xcc\xae\x65\xcc\xb8\xcd\x86\xcc\x84\xcd\x89\xcd\x99\\baz)".LoadIniString().General.foo);
        }
        
    }
}
