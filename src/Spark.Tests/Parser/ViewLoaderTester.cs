// Copyright 2008-2009 Louis DeJardin - http://whereslou.com
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// 
using System.IO;
using System.Linq;
using NUnit.Framework.SyntaxHelpers;
using Spark.Parser;
using NUnit.Framework;
using Rhino.Mocks;
using Spark.FileSystem;
using Spark.Compiler;
using Spark.Parser.Syntax;

namespace Spark.Tests.Parser
{
    [TestFixture]
    public class ViewLoaderTester
    {

        private ViewLoader loader;

        private IViewFolder viewSourceLoader;
        private ISparkSyntaxProvider syntaxProvider;

        [SetUp]
        public void Init()
        {

            viewSourceLoader = MockRepository.GenerateMock<IViewFolder>();
            viewSourceLoader.Stub(x => x.ListViews("home")).Return(new[] { "file.spark", "other.spark", "_comment.spark" });
            viewSourceLoader.Stub(x => x.ListViews("Home")).Return(new[] { "file.spark", "other.spark", "_comment.spark" });
            viewSourceLoader.Stub(x => x.ListViews("Account")).Return(new[] { "index.spark" });
            viewSourceLoader.Stub(x => x.ListViews("Shared")).Return(new[] { "layout.spark", "_header.spark", "default.spark", "_footer.spark" });

            syntaxProvider = MockRepository.GenerateMock<ISparkSyntaxProvider>();

            loader = new ViewLoader { ViewFolder = viewSourceLoader, SyntaxProvider = syntaxProvider };
        }

        IViewFile ExpectGetChunks(string path, params Chunk[] chunks)
        {
            var source = MockRepository.GenerateMock<IViewFile>();

            viewSourceLoader.Expect(x => x.GetViewSource(path)).Return(source);
            source.Expect(x => x.LastModified).Return(0);
            syntaxProvider.Expect(x => x.GetChunks(null, null)).IgnoreArguments().Return(chunks);

            return source;
        }

        [Test]
        public void LoadSimpleFile()
        {
            ExpectGetChunks("home\\simple.spark", new SendLiteralChunk());
            viewSourceLoader.Stub(x => x.HasView("home\\_global.spark")).Return(false);
            viewSourceLoader.Stub(x => x.HasView("Shared\\_global.spark")).Return(false);

            var chunks = loader.Load("home\\simple.spark");
            viewSourceLoader.VerifyAllExpectations();
            syntaxProvider.VerifyAllExpectations();

            Assert.AreEqual(1, chunks.Count());
            Assert.AreEqual(1, loader.GetEverythingLoaded().Count());
        }

        [Test]
        public void LoadUsedFile()
        {
            ExpectGetChunks("Home\\usefile.spark", new RenderPartialChunk { Name = "mypartial" });
            viewSourceLoader.Expect(x => x.HasView("Home\\mypartial.spark")).Return(true);
            ExpectGetChunks("Home\\mypartial.spark", new SendLiteralChunk { Text = "Hello world" });
            viewSourceLoader.Stub(x => x.HasView("Home\\_global.spark")).Return(false);
            viewSourceLoader.Stub(x => x.HasView("Shared\\_global.spark")).Return(false);

            loader.Load("Home\\usefile.spark");
            viewSourceLoader.VerifyAllExpectations();
            syntaxProvider.VerifyAllExpectations();

            Assert.AreEqual(2, loader.GetEverythingLoaded().Count());
        }


        [Test]
        public void LoadSharedFile()
        {
            ExpectGetChunks("Home\\usefile.spark", new RenderPartialChunk { Name = "mypartial" });
            viewSourceLoader.Expect(x => x.HasView("Home\\mypartial.spark")).Return(false);
            viewSourceLoader.Expect(x => x.HasView("Shared\\mypartial.spark")).Return(true);
            ExpectGetChunks("Shared\\mypartial.spark", new SendLiteralChunk { Text = "Hello world" });

            viewSourceLoader.Stub(x => x.HasView("Home\\_global.spark")).Return(false);
            viewSourceLoader.Stub(x => x.HasView("Shared\\_global.spark")).Return(false);

            loader.Load("Home\\usefile.spark");
            viewSourceLoader.VerifyAllExpectations();
            syntaxProvider.VerifyAllExpectations();
        }

        [Test, Ignore("This test is invalidated. Mocks are hard to keep 'current'.")]
        public void FindPartialFiles()
        {
            var partials3 = loader.FindPartialFiles("Home\\other.spark");
            var partials2 = loader.FindPartialFiles("Account\\index.spark");
            viewSourceLoader.VerifyAllExpectations();
            syntaxProvider.VerifyAllExpectations();

            Assert.AreEqual(3, partials3.Count);
            Assert.That(partials3.Contains("comment"));
            Assert.That(partials3.Contains("header"));
            Assert.That(partials3.Contains("footer"));


            Assert.AreEqual(2, partials2.Count);
            Assert.That(partials2.Contains("header"));
            Assert.That(partials2.Contains("footer"));
        }

        [Test, ExpectedException(typeof(FileNotFoundException))]
        public void FileNotFoundException()
        {
            viewSourceLoader.Expect(x => x.GetViewSource("Home\\nosuchfile.spark")).Throw(new FileNotFoundException());

            loader.Load("Home\\nosuchfile.spark");
            viewSourceLoader.VerifyAllExpectations();
            syntaxProvider.VerifyAllExpectations();
        }

        [Test]
        public void ExpiresWhenFilesChange()
        {
            var source = ExpectGetChunks("home\\changing.spark", new SendLiteralChunk { Text = "Hello world" });
            viewSourceLoader.Expect(x => x.GetViewSource("home\\changing.spark")).Return(source);
            source.Expect(x => x.LastModified).Return(0);
            viewSourceLoader.Expect(x => x.GetViewSource("home\\changing.spark")).Return(source);
            source.Expect(x => x.LastModified).Return(42);

            viewSourceLoader.Stub(x => x.HasView("home\\_global.spark")).Return(false);
            viewSourceLoader.Stub(x => x.HasView("Shared\\_global.spark")).Return(false);

            loader.Load("home\\changing.spark");
            Assert.That(loader.IsCurrent());
            Assert.That(!loader.IsCurrent());
            viewSourceLoader.VerifyAllExpectations();
            syntaxProvider.VerifyAllExpectations();
        }

        [Test]
        public void LoadingPartialInsideNamedSection()
        {
            var viewFolder = new InMemoryViewFolder
                                 {
                                     {"home\\index.spark", "<for each='var x in new[]{1,2,3}'><Guts><section:foo><Another/></section:foo></Guts></for>"},
                                     {"home\\_Guts.spark", "<div><render:foo/></div>"},
                                     {"home\\_Another.spark", "<p>hello world</p>"}
                                 };
            var viewLoader = new ViewLoader { SyntaxProvider = new DefaultSyntaxProvider(ParserSettings.DefaultBehavior), ViewFolder = viewFolder };
            var chunks = viewLoader.Load("home\\index.spark");
            var everything = viewLoader.GetEverythingLoaded();
            Assert.AreEqual(3, everything.Count());
        }

        [Test]
        public void PartialsInSameFolderAreDiscovered()
        {
            var viewFolder = new InMemoryViewFolder
                             {
                                 {"home\\zero.spark", ""},
                                 {"home\\_one.spark", ""},
                                 {"product\\two.spark", ""},
                                 {"product\\_three.spark", ""},
                                 {"product\\_four.spark", ""},
                                 {"invoice\\five.spark", ""},
                             };

            var viewLoader = new ViewLoader { ViewFolder = viewFolder };
            var zero = viewLoader.FindPartialFiles("home\\zero.spark");
            var product = viewLoader.FindPartialFiles("product\\two.spark");
            var invoice = viewLoader.FindPartialFiles("invoice\\five.spark");

            Assert.That(zero.Count(), Is.EqualTo(1));
            Assert.That(zero, Has.Some.EqualTo("one"));

            Assert.That(product.Count(), Is.EqualTo(2));
            Assert.That(product, Has.Some.EqualTo("three"));
            Assert.That(product, Has.Some.EqualTo("four"));

            Assert.That(invoice.Count(), Is.EqualTo(0));

            zero = viewLoader.FindPartialFiles("home/zero.spark");
            product = viewLoader.FindPartialFiles("product/two.spark");
            invoice = viewLoader.FindPartialFiles("invoice/five.spark");

            Assert.That(zero.Count(), Is.EqualTo(1));
            Assert.That(zero, Has.Some.EqualTo("one"));

            Assert.That(product.Count(), Is.EqualTo(2));
            Assert.That(product, Has.Some.EqualTo("three"));
            Assert.That(product, Has.Some.EqualTo("four"));

            Assert.That(invoice.Count(), Is.EqualTo(0));
        }

        [Test]
        public void PartialsInCascadingBaseFoldersAndSharedFoldersAreDiscovered()
        {
            var viewFolder = new InMemoryViewFolder
                             {
                                 {"area1\\controller2\\view3.spark", ""},
                                 {"area1\\controller2\\Shared\\_alpha.spark", ""},
                                 {"area1\\Shared\\_beta.spark", ""},
                                 {"Shared\\_gamma.spark", ""},
                                 {"area1\\controller2\\_epsilon.spark", ""},
                                 {"area1\\_zeta.spark", ""},
                                 {"_eta.spark", ""},
                                 {"area1\\controller4\\_dontfind1.spark", ""},
                                 {"area1\\controller4\\Shared\\_dontfind2.spark", ""},
                                 {"area2\\Shared\\_dontfind3.spark", ""},
                             };

            var viewLoader = new ViewLoader { ViewFolder = viewFolder };

            var partials = viewLoader.FindPartialFiles("area1\\controller2\\view3.spark");
            Assert.That(partials, Has.Some.EqualTo("alpha"));
            Assert.That(partials, Has.Some.EqualTo("beta"));
            Assert.That(partials, Has.Some.EqualTo("gamma"));
            Assert.That(partials, Has.Some.EqualTo("epsilon"));
            Assert.That(partials, Has.Some.EqualTo("zeta"));
            Assert.That(partials, Has.Some.EqualTo("eta"));
            Assert.That(partials, Has.None.EqualTo("dontfind1"));
            Assert.That(partials, Has.None.EqualTo("dontfind2"));
            Assert.That(partials, Has.None.EqualTo("dontfind3"));
        }
    }
}