Semantic Web Library for C#/.NET
================================

By Joshua Tauberer <tauberer@for.net>

http://taubz.for.net/code/semweb

Copyright 2006 Joshua Tauberer.  This package is released under the 
terms of the Creative Commons Attribution License:

	http://creativecommons.org/licenses/by/2.0/

The license basically means you can copy, distribute and modify this 
package however you like, but you need to give me due credit.  I think 
that's fair.  My interpretation of this is that it applies to both the 
source and binary form of the library.

For more information, view doc/index.html and the API documentation
in apidocs/index.html.

BUILD INSTRUCTIONS

Run make if you're in Linux.  Nothing complicated here.  You'll need 
Mono installed (and the ByteFx and Sqlite Client DLLs for SQL database 
support).

A MonoDevelop solution file (semweb.mds) is included too.

If you're in Windows, I don't have Visual Studio project files for you. 
Just throw the .cs files in the src directory into a new project and 
compile. You'll need to reference System.XML, sparql-core.dll in the
bin directory, and maybe other things I'm forgetting.

If you include the MySQL and SQLite .cs files, you'll need to reference 
Mono's ByteFx and Sqlite Client assemblies (see www.mono-project.com).  
Otherwise just leave out those .cs files.

OTHER INCLUDED FILES

bin/sparql-core.dll is based on the SPARQL Engine by Ryan Levering,
which is covered by the GNU LGPL.  The original Java JAR was
coverted to a .NET assembly using IKVM (see below).
See: http://sparql.sourceforge.net/

bin/IKVM*.dll are auxiliary assemblies for running the SPARQL
engine.  IKVM was written by Jeroen Frijters.  See http://www.ikvm.net.

