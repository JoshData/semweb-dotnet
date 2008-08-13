This directory is used to build sparql-core.dll from Ryan Levering's
SPARQL Engine library.

I have modified the library significantly to take advantage of aspects 
of SemWeb that make queries faster to execute. The changes are published 
as a patch to Ryan's latest SVN version.

To build sparql-core.dll:

Check out the upstream code into the work-copy directory here as:

svn co https://sparql.svn.sourceforge.net/svnroot/sparql/engine/trunk work-copy

Make a few changes to make it build, and build it to generate some files:

cd work-copy
svn rm src/main/name/levering/ryan/sparql/parser/JavaCharStream.java 
mkdir dist
ant

Then apply the patch:

patch -u -p1 < ../local-changes.diff

The build it again to make sure the build succeeds:

ant
cd ..

Then run make, which runs ant to build the Java library and then IKVM to 
turn the Java jar into a .NET dll. You will need to have ant and IKVM 
installed somewhere. You may need to adjust the paths to Java and IKVM 
in the Makefile. I use IKVM version 0.34.0.2.
