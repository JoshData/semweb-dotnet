BUILDING THE EXAMPLES

If you're in Linux, just run make to compile all the examples.

Each of the files **.cs in this directory is meant to be compiled on is own.  
A description of each program is at the top of each source file.

If you're using Mono, it'll help to copy or symlink the SemWeb.dll assembly 
into this directory:     ln -s ../bin/SemWeb.dll .
To compile an example:   mcs example.cs -r:SemWeb.dll
To run it:               mono example.exe

If you're using Visual Studio, create a new project, reference SemWeb.dll in 
the bin directory, and add the exampleXX.cs file to the project.

EXAMPLE RDF DATA

In linux, run "./getsomedata.sh" to download some RDF data files.  You'll
end up with four files about the U.S. Congress: people.rdf, bills.2005.rdf,
bills.2005.cosponsors.rdf, and also congress.n3 which contains everything
in the first three.

Otherwise, you can grab the files at:
	http://www.govtrack.us/data/rdf/people.rdf.gz
	http://www.govtrack.us/data/rdf/bills.2005.rdf.gz and
	http://www.govtrack.us/data/rdf/bills.2005.cosponsors.rdf.gz
You'll need to un-gzip them.  You might also want to merge them
into a single file with:
	mono ../bin/rdfstorage.exe --out n3:congress.n3 people.rdf bills.2005.rdf bills.2005.cosponsors.rdf

