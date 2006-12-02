#!/bin/sh

# This downloads some RDF data about the U.S. Congress.

echo Download RDF file from GovTrack.us...

wget -nc http://www.govtrack.us/data/rdf/people.rdf.gz
wget -nc http://www.govtrack.us/data/rdf/bills.109.rdf.gz
wget -nc http://www.govtrack.us/data/rdf/bills.109.cosponsors.rdf.gz

echo Uncompressing them...

gunzip people.rdf.gz
gunzip bills.109.rdf.gz
gunzip bills.109.cosponsors.rdf.gz

echo Merging them together into congress.n3...

mono ../bin/rdfstorage.exe --out n3:congress.n3 \
	people.rdf bills.109.rdf bills.109.cosponsors.rdf
