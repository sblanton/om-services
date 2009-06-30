#!/bin/perl
# This script accepts two parameters, the pcc tar file name and a text file that 
# lists the files to be tarred up.

if ($#ARGV < 1) {
  print "ERROR: You must pass in two parameters: 1) the name of the tar and 2) the name of the text file that lists the contents of the tar.\n";
  print "EXAMPLE: inet_pcctgt_generator.pl mypcc.tar filelist.txt\n";
  exit 1;
}
else {
  print "pcc tar file will be named: $ARGV[0]\n";
  print "Dependencies will be read from file: $ARGV[1]\n";
}

$project = "MY_MEISTER_PROJECT";
$target = $ARGV[0];
$tgtFile = "$target" . ".tgt";

$fileList = $ARGV[1];
open(DEP,$fileList)
  or die "ERROR: Unable to open $fileList!\n";
print "Found $fileList, proceeding.\n";


@dependencies = <DEP>;
close(DEP);

$TGT =<<TGTSTART;
<?xml version="1.0" ?> 
<OMTarget>
  <Version>6.3</Version> 
  <Name>$target</Name> 
  <Project>$project</Project> 
  <TargetDefinitionFile>$tgtFile</TargetDefinitionFile> 
  <OSPlatform>Windows</OSPlatform> 
  <BuildType>DFS Package Files</BuildType> 
  <IntDirectory /> 
  <PhoneyTarget>false</PhoneyTarget> 
  <BuildTask>
    <Name>Copy Files</Name> 
    <OptionGroup>
      <GroupName>Build Task Options</GroupName> 
      <Type>0</Type> 
    </OptionGroup>
  </BuildTask>
  <BuildTask>
    <Name>Package Files</Name> 
    <OptionGroup>
      <GroupName>Build Task Options</GroupName> 
      <Type>0</Type> 
    </OptionGroup>
  </BuildTask>
TGTSTART

foreach $dep (@dependencies) {
  # Remove end of line character
  $dep =~ s/\n//;
  # Remove leading spaces
  $dep =~ s/^\s+//;
  # Remove trailing spaces
  $dep =~ s/\s+$//;
  # Add dependency only if not an empty line
  if (! $dep eq "") {
  $TGT .=<<TGTDEP;
  <Dependency>
    <Name>$dep</Name> 
    <Type>5</Type> 
    <ParentBuildTask>Copy Files</ParentBuildTask> 
    <ParentOptionGroup>Build Task Options</ParentOptionGroup> 
  </Dependency>
TGTDEP
}
}

$TGT .=<<TGTEND;
  <Dependency>
    <Name>Copy Files results</Name> 
    <Type>6</Type> 
    <ParentBuildTask>Package Files</ParentBuildTask> 
    <ParentOptionGroup>Build Task Options</ParentOptionGroup> 
  </Dependency>
</OMTarget>
TGTEND

# Write TGT to a file
open(TGT, ">$tgtFile");

print TGT "$TGT\n";

close (TGT);
