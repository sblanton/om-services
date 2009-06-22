*********
GenVSTGTs.exe is a command line executable designed to automate the generation of Visual Studio 2005/2008 TGTs used by the OpenMake build process. It is
primarily useful for organizations which have very sophisticated multi-solution/multi-project applications that may be too time consuming and/or 
difficult to integrate using the OpenMake supplied Visual Studio Add-In. It may also be useful for customers interested in automating the TGT generation 
prior to running a build or Workflow, for example, in a Continuous Integration setting where a Workflow is triggered by a change event.

GenVSTGTs.exe works by using a config file, 'GenVSTGTs.config', to feed in key settings information required for generating TGTs. This includes some of 
the same kind of information supplied to the Visual Studio Add-In, such as an OpenMake Project name, Build Service mappings, output directories, etc. In 
addition, it includes built in scanning controls that allow for specifying flat or recursive scanning of directories for Project and Solution  
dependencies as well as include/exclude patterns and an incremental analyzer that determines whether or not a TGT needs to be recreated based on 
Project/Solution timestamps.

In addition to passing arguments to the generator through the configuration file, override arguments may also be passed in from the command line to 
control the scanning and generation behavior. This will likely be needed in cases where TGTs are generated for multiple applications with separate, 
unique OpenMake project names. In such a case, passing in the OMProject=<PROJECT NAME> could be run for each application, overriding the setting in the 
configuration file.

In order to function correctly, GenVSTGTS requires the existance of Visual Studio 2005 or 2008 and their corresponding frameworks (2.0/3.5). 

*********
Below is the USAGE statement for GenVSTGTs which appears when running with a -h, /h, -?, or /? as well as when an invalid argument is passed in.

USAGE: GenVSTGTs is a Visual Studio 2005/2008 OpenMake TGT Generator. It utilizes the 'GenVSTGTs.config' file (found in the utility's 'bin' directory)
to identify common attributes required to generate TGTs, such as Build Service mapping information, OM Project defaults, scanning modes, and include or
exclude dependency patterns. You may open the 'GenVSTGTs.config' file in a text editor for viewing and editing default settings. All arguments in the
'GenVSTGTs.config' are based on key words beginning with the @@ signs. The values are in the lines that follow each of the key words.

It is possible to override any of the settings in the configuration file which have only one value (most are singular) by passing in any of the key
names, followed by an equals (=) sign, followed by the desired value.

EXAMPLE: To run the TGT Generator in the non-default 'Debug' configuration with recursive scanning turned off for the Project 'SAMPLE VSBUILD', one
would run the following GenVSTGTs command:

     GenVSTGTs CFG=DEBUG Recursive=N OMProject="SAMPLE VSBUILD"

*********
Below is information on defining the GenVSTGTS.config file. Please view the file itself for detailed comments on all of the acceptable settings.

Description: GenVSTGT.config is a configuration file that partners with the GenVSTGT.exe executable. This configuration file must be found in the same 
directory where GenVSTGT.exe lives (typically OpenMake's client\bin directory). It's purpose is to provide inputs to the command line TGT generator 
which qualify the nature of its scanning for dependencies to the TGTs (namely project and/or solution files) and the generating of TGTs. Listed below 
are keys (beginning with @@ signs) which are provided values that get interpreted by the command line executable. All descriptions for the keys are 
provided in the comment sections above them. 

Some of the keys are required to have value (e.g., 'OMProject') whereas others are optional (e.g., 'Debug'). If optional values are not provided, 
'GenVSTGTs.exe' will use its own defaults (listed in commented sections below). Even in the cases where a key's value is required, that value may be 
provided by the command line. The majority of keys values, other than FileTypes, BuildServiceMappings and Includes/Excludes may be passed in from the 
command line. For example, the following call would override the default configuration of RELEASE to Debug (CFG=DEBUG), turn off recursive scanning 
(Recursive=N) and override any default OpenMake Project value set here (OMProject="SAMPLE VSBUILD").

	GenVSTGTs CFG=DEBUG Recursive=N OMProject="SAMPLE VSBUILD"

Note that any line beginning with a pound (#) sign causes GenVSTGT.exe to exclude it from processing. This is useful both for creating comments as well 
as turning off optional defaults.
