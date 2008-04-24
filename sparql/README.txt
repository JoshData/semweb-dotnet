This directory is used to build sparql-core.dll from Ryan Levering's
SPARQL Engine library.

I have modified the library significantly to take advantage of aspects 
of SemWeb that make queries faster to execute. The changes are published 
as a patch to Ryan's latest SVN version.

To build sparql-core.dll:

Check out the upstream code into the work-copy directory here as:

svn co https://sparql.svn.sourceforge.net/svnroot/sparql/engine/trunk work-copy

Then apply the patch:

patch -p0 work-copy < local-changes.diff

Then run make, which runs ant to build the Java library and then IKVM to 
turn the Java jar into a .NET dll. You will need to have ant and IKVM 
installed somewhere. You may need to adjust the paths to Java and IKVM 
in the Makefile. I use IKVM version 0.34.0.2.
