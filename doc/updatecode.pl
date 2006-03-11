#!/usr/bin/perl

opendir D, ".";
foreach $f (readdir(D)) {
	if ($f !~ /\.html$/) { next; }
	$content = `cat $f`;
	
	$content =~ s/<pre class="code" file="([^"]+?)">([^<]*?)<\/pre>/"<pre class=\"code\" file=\"$1\">" . GetFile($1) . "<\/pre>"/egi;
	
	open F, ">$f";
	print F $content;
	close F;
}
closedir D;

sub GetFile {
	print "$f <-- $_[0]\n";
	my $d = `cat $_[0]`;
	$d =~ s/\&/\&amp;/g;
	$d =~ s/</\&lt;/g;
	$d =~ s/>/\&gt;/g;
	return $d;
}
