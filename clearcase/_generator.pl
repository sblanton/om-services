#!/usr/bin/perl

use Cwd;

if ($#ARGV < 1) {
  print "ERROR: You must pass in your view tag and at least one activity ID.\n";
  print "USAGE: mrchinet_filelist_generator.pl <view tag> <activity ID 1> <activity ID 2> ...\n";
  print "EXAMPLE: inet_filelist_generator.pl racfid_pcc_1.0_PRJ  May2006_Fix May2006_Release ...\n";
  exit 1;
}

$pvob = "my_pvob";
$view = @ARGV[0];
shift @ARGV;
$Cwd = getcwd()  or die "Could not find current working directory.\n";
$filelist = "$Cwd\\filelist.txt";

# For each activity, push the versions into an array.
@VERSIONS = ();
foreach $activity (@ARGV) {
  chdir "L:\\$view" or die "Cannot find view directory L:\\$view: $!\n";
  $versionlist = `cleartool lsactivity -fmt "\%[versions]p" activity:$activity@\\$pvob`  or die "Versions could not be retrieved for $activity. Aborting script.\n";
  @VERSIONLIST = split(' ',$versionlist);
  push (@VERSIONS, @VERSIONLIST); 
}

# Format the version information provided by ClearCase.
@FORMATTEDVERSIONS = ();
foreach $version (@VERSIONS) {
  # Strip off everything after @
  $version =~ s/\@.*$//;
  # Exclude everything up to the first occurence of "\source"
  $version =~ s/^.*\\source//;
  # Strip off the leading backslash
  $version =~ s/^\\//;
  # Include only filenames with extensions, thereby eliminating directories.
  if ($version =~ /\..*$/) {
    push (@FORMATTEDVERSIONS, $version);
  }
}

# Extract out unique elements
@ELEMENTS = ();
%seen = ();
foreach $version (@FORMATTEDVERSIONS) {
  push(@ELEMENTS, $version) unless $seen{$version}++;
}

# Generate filelist.txt

open(FILELIST, "> $filelist")  or die "Couldn't open file $Cwd\\$filelist: $!";

foreach $elem (@ELEMENTS) {
  print "$elem\n";
  print FILELIST "$elem\n";
}
close(FILELIST)  or die "Filehandle FILELIST didn't close: $!";
