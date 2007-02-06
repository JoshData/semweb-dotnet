SemWeb: A Semantic Web Library for C#/.NET
==========================================

By Joshua Tauberer <http://razor.occams.info>

http://razor.occams.info/code/semweb

BUILD INSTRUCTIONS
------------------

Run make if you're in Linux.  Nothing complicated here.  You'll need
Mono installed (and the MySQL/Connector and Sqlite Client DLLs for SQL 
database support, optionally).  It'll build .NET 1.1 binaries to the
bin directory and .NET 2.0 binaries with generics to the bin_generics
directory.

A MonoDevelop solution file (semweb.mds) and a Visual Studio 2005 solution
file (SemWeb.sln) are included too.  They build .NET 2.0 binaries with
generics to the bin_generics directory.

If you build the MySQL and SQLite .cs files, you'll need to reference 
MySQL's MySql.Data.dll and Sqlite Client assemblies (see www.mono-project.com).  Otherwise just leave out those .cs files.
Put MySql.Data.dll in a "lib" directory within the SemWeb directory.

The sources are set up with a conditional compilation flag "DOTNET2" so 
that the sources can be compiled for both .NET 1.1 and 2.0, taking 
advantage of generics.

IMPORTED FILES FROM OTHER PROJECTS & CREDITS
--------------------------------------------

sparql-core.dll is based on the SPARQL Engine by Ryan Levering,
which is covered by the GNU LGPL.  The original Java JAR was
coverted to a .NET assembly using IKVM (see below).  Actually, I've
made numerous changes to the library so it can take advantage of
faster API paths in SemWeb.
See: http://sparql.sourceforge.net/

IKVM*.dll are auxiliary assemblies for running the SPARQL
engine.  IKVM was written by Jeroen Frijters.  See http://www.ikvm.net.

Euler.cs is adapted from Jos De Roo's JavaScript Euler inferencing
engine.  See: http://www.agfa.com/w3c/euler/

LICENSE
-------

Copyright 2007 Joshua Tauberer.  This package is released under the 
terms of the Creative Commons Attribution License:

	http://creativecommons.org/licenses/by/2.0/

The license basically means you can copy, distribute and modify this 
package however you like, but you need to give me due credit.  I think 
that's fair.  My interpretation of this is that it applies to both the 
source and binary form of the library.

For more information, view doc/index.html and the API documentation
in apidocs/index.html.

