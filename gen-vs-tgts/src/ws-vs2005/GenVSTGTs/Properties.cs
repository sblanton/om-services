using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Reflection;

namespace GenVSTGTs
{
    public class Properties
    {
        private Dictionary<String, String[]> properties;
        private MessageHandler mh;
        private Dictionary<String, String> cmdArgs;
        public Properties(String[] args)
        {
            MessageHandler tmpMh = new MessageHandler(false);
            foreach (String arg in args)
                if (arg.StartsWith("-h", StringComparison.InvariantCultureIgnoreCase) || arg.StartsWith("/h", StringComparison.InvariantCultureIgnoreCase) || arg.StartsWith("-?") || arg.StartsWith("/?"))
                {
                    tmpMh.printUsage();
                    tmpMh.exitSuccess("");
                }
            //get full path to execution directory where config file lives
            String pathToExe = getExePath();
            String configFile = pathToExe + Path.DirectorySeparatorChar + "GenVSTGTs.config";
            if (!File.Exists(configFile))
                tmpMh.exitError("\nGenVSTGTs exited with errors: 'GenVSTGTs.config' Congifuration File not found in path: '" + pathToExe + "'");
            else
                getProperties(configFile); //gets properties and puts them into global properties Dictionary variable
            processCmdArgs(args);
        }

        public String getExePath()
        {
            String exe = Assembly.GetExecutingAssembly().Location;
            String path = Path.GetDirectoryName(exe);
            return path;
        }

        public MessageHandler getMessageHandler()
        {
            if (this.mh == null)
                this.mh = new MessageHandler(this.isDebug());
            return this.mh;
        }

        public string getBaseDir()
        {
            String baseDir = getArgValue("BaseDir");
            if (baseDir == null)
                baseDir = Environment.GetEnvironmentVariable("BaseDir");
            if (baseDir == null)
            {
                String[] props = new String[0];
                if (properties.ContainsKey("BaseDir"))
                    props = properties["BaseDir"];
                if (props.Length > 0)
                    baseDir = props[0];
                else
                {
                    baseDir = ".";
                    mh.warn("No @@BaseDir value detected in VSTGTGen Congifuration File. Using default: " + baseDir);
                    cmdArgs.Add("BaseDir", baseDir);
                }
            }
            if (!Directory.Exists(baseDir))
            {
                mh.exitError("Invalid base directory \"" + baseDir + "\"");
            }
            baseDir = Regex.Replace(baseDir, @"/", "\\"); //all slashes back
            baseDir = Regex.Replace(baseDir, @"\\$", ""); //strip trailing slashes
            return baseDir.Trim();
        }

        public string getTGTDir()
        {
            String TGTDir = getArgValue("TGTDir");
            if (TGTDir == null)
                TGTDir = Environment.GetEnvironmentVariable("TGTDir");
            if (TGTDir == null)
            {
                TGTDir = "";
                String[] props = new String[0];
                if (properties.ContainsKey("TGTDir"))
                    props = properties["TGTDir"];
                if (props.Length > 0)
                    TGTDir = props[0];
                else
                    mh.debugPrint("@@TGTDir value not detected in VSTGTGen Congifuration File. Using default Solution or Project dirs.");
            }
            if (!Directory.Exists(TGTDir) && !TGTDir.Equals(""))
            {
                try
                {
                    mh.warn("TGTDir Directory \"" + TGTDir + "\" not found on file system. Creating directory.");
                    Directory.CreateDirectory(TGTDir);
                }
                catch (Exception e)
                {
                    mh.exitError("TGT Directory could not be created!\n" + e.Message);
                }
            }
            TGTDir = Regex.Replace(TGTDir, @"/", "\\"); //all slashes back
            TGTDir = Regex.Replace(TGTDir, @"\\$", ""); //strip trailing slashes
            return TGTDir.Trim();
        }

        public string getOMProject()
        {
            String omProject = getArgValue("OMProject");
            if (omProject == null)
                omProject = Environment.GetEnvironmentVariable("OMProject");
            if (omProject == null)
            {
                String[] props = new String[0];
                if (properties.ContainsKey("OMProject"))
                    props = properties["OMProject"];
                if (props.Length > 0) //eventually add omapi connection to validate project
                    omProject = props[0];
                else
                {
                    mh.exitError("No valid @@OMProject value detected in VSTGTGen Congifuration File. Add to Configuration File or pass in OMProject=<PROJECT> to command line call.");
                }
            }
            return omProject.Trim();
        }

        public string getConfig()
        {
            String config = getArgValue("CFG");
            if (config == null)
                config = Environment.GetEnvironmentVariable("CFG");
            if (config == null)
            {
                String[] props = new String[0];
                if (properties.ContainsKey("CFG"))
                    props = properties["CFG"];
                if (props.Length > 0)
                    config = props[0];
                else
                {
                    config = "RELEASE";
                    mh.warn("No valid @@CFG value detected in VSTGTGen Congifuration File. Using default: " + config);
                    cmdArgs.Add("CFG", "RELEASE");
                }
            }
            return config.Trim();
        }
        public String[] getFileTypes()
        {
            String[] fileTypes;
            String[] props = new String[0];
            if (properties.ContainsKey("FileTypes"))
                props = properties["FileTypes"];
            if (props.Length > 0)
                fileTypes = props;
            else
            {
                fileTypes = new String[] { ".sln", ".csproj", ".vbproj", ".vcproj" };
                String message = "";
                foreach (String fileType in fileTypes)
                    message += "\n  " + fileType;
                mh.warn("@@FileTypes value(s) not detected in VSTGTGen Congifuration File. Using default: " + message);
            }
            return fileTypes;
        }

        public String[] getIncludes()
        {
            String[] includes = null;
            String[] props = new String[0];
            if (properties.ContainsKey("Includes"))
                props = properties["Includes"];
            if (props.Length > 0)
                includes = props;
            else if (isDebug())
            {
                Console.WriteLine("\nNo explicit Includes detected in VSTGTGen Congifuration File.");
            }
            return includes;
        }

        public String[] getExcludes()
        {
            String[] excludes = null;
            String[] props = new String[0];
            if (properties.ContainsKey("Excludes"))
                props = properties["Excludes"];
            if (props.Length > 0)
                excludes = props;
            else if (isDebug())
            {
                Console.WriteLine("\nNo explicit Excludes detected in VSTGTGen Congifuration File.");
            }
            return excludes;
        }

        public Dictionary<String, String> getBuildServiceMappings()
        {
            String[] buildTypeLines;
            Dictionary<String, String> buildTypeMappings;
            String[] props = new String[0];
            if (properties.ContainsKey("BuildServiceMappings"))
                props = properties["BuildServiceMappings"];
            if (props.Length > 0)
                buildTypeLines = props;
            else
            {
                buildTypeLines = new String[] { ".dll=.Net 2005 Solution Dynamic Link Library", ".exe=.Net 2005 Solution Executable", ".lib=.Net 2005 Solution Library" };
                String message = "";
                foreach (String buildTypeLine in buildTypeLines)
                    message += "\n  " + buildTypeLine;
                mh.warn("@@BuildTypeMappings value(s) not detected in VSTGTGen Congifuration File. Using default: " + message);
            }
            //place into a dictionary where target ext is the key and build type is value
            buildTypeMappings = new Dictionary<String, String>();
            String[] tmpArgs = new String[2];
            foreach (String btm in buildTypeLines)
            {
                tmpArgs = btm.Split('=');
                buildTypeMappings.Add(tmpArgs[0].Trim(), tmpArgs[1].Trim());
            }
            return buildTypeMappings;
        }

        public bool isRecursive()
        {
            bool isRecursive = false;
            String prop = getArgValue("Recursive");
            if (prop == null)
                prop = Environment.GetEnvironmentVariable("Recursive");
            if (prop == null)
            {
                String[] props = new String[0];
                if (properties.ContainsKey("Recursive"))
                    props = properties["Recursive"];
                if (props.Length > 0)
                    prop = props[0];
                else
                {
                    mh.warn("@@Recursive value not detected in VSTGTGen Congifuration File. Using default: N");
                    prop = "N";
                    cmdArgs.Add("Recursive", "N");
                }
            }
            if (prop.IndexOf("Y", StringComparison.InvariantCultureIgnoreCase) >= 0)
            {
                isRecursive = true;
            }
            else if (prop.IndexOf("N", StringComparison.InvariantCultureIgnoreCase) >= 0)
            {
                isRecursive = false;
            }
            return isRecursive;
        }

        public bool isIncremental()
        {
            bool isIncremental = true;
            String prop = getArgValue("Incremental");
            if (prop == null)
                prop = Environment.GetEnvironmentVariable("Incremental");
            if (prop == null)
            {
                String[] props = new String[0];
                if (properties.ContainsKey("Incremental"))
                    props = properties["Incremental"];
                if (props.Length > 0)
                    prop = props[0];
                else
                {
                    mh.warn("@@Incremental value not detected in VSTGTGen Congifuration File. Using default: Y");
                    prop = "Y";
                    cmdArgs.Add("Incremental", "Y");
                }
            }
            if (prop.IndexOf("Y", StringComparison.InvariantCultureIgnoreCase) >= 0)
            {
                isIncremental = true;
            }
            else if (prop.IndexOf("N", StringComparison.InvariantCultureIgnoreCase) >= 0)
            {
                isIncremental = false;
            }
            return isIncremental;
        }

        public bool isSlnBuild()
        {
            bool isSlnBuild = true;
            String prop = getArgValue("SolutionBuild");
            if (prop == null)
                prop = Environment.GetEnvironmentVariable("SolutionBuild");
            if (prop == null)
            {
                String[] props = new String[0];
                if (properties.ContainsKey("SolutionBuild"))
                    props = properties["SolutionBuild"];
                if (props.Length > 0)
                    prop = props[0];
                else
                {
                    mh.warn("@@SolutionBuild value not detected in VSTGTGen Congifuration File. Using default: Y");
                    prop = "Y";
                    cmdArgs.Add("SolutionBuild", "Y");
                }
            }
            if (prop.IndexOf("Y", StringComparison.InvariantCultureIgnoreCase) >= 0)
            {
                isSlnBuild = true;
            }
            else if (prop.IndexOf("N", StringComparison.InvariantCultureIgnoreCase) >= 0)
            {
                isSlnBuild = false;
            }
            return isSlnBuild;
        }

        public bool isRel2BaseDir()
        {
            bool isRel2BaseDir = false;
            String prop = getArgValue("Rel2BaseDir");
            if (prop == null)
                prop = Environment.GetEnvironmentVariable("Rel2BaseDir");
            if (prop == null)
            {
                String[] props = new String[0];
                if (properties.ContainsKey("Rel2BaseDir"))
                    props = properties["Rel2BaseDir"];
                if (props.Length > 0)
                    prop = props[0];
                else
                {
                    mh.debugPrint("@@Rel2BaseDir value not detected in VSTGTGen Congifuration File. Using default: N");
                    prop = "N";
                    cmdArgs.Add("Rel2BaseDir", "N");
                }
            }

            if (prop.IndexOf("Y", StringComparison.InvariantCultureIgnoreCase) >= 0)
            {
                isRel2BaseDir = true;
            }
            else if (prop.IndexOf("N", StringComparison.InvariantCultureIgnoreCase) >= 0)
            {
                isRel2BaseDir = false;
            }
            return isRel2BaseDir;
        }

        public bool isDebug()
        {

            bool isDebug = false;
            String prop = getArgValue("Debug");
            if (prop == null)
                prop = Environment.GetEnvironmentVariable("Debug");
            if (prop == null)
            {
                String[] props = new String[0];
                if (properties.ContainsKey("Debug"))
                    props = properties["Debug"];
                if (props.Length > 0)
                    prop = props[0];
                else
                    prop = "N";
            }
            if (prop.IndexOf("Y", StringComparison.InvariantCultureIgnoreCase) >= 0)
            {
                isDebug = true;
            }
            else if (prop.IndexOf("N", StringComparison.InvariantCultureIgnoreCase) >= 0)
            {
                isDebug = false;
            }
            return isDebug;
        }

        private void getProperties(String configFile)
        {
            Console.WriteLine("Processing \"" + configFile + "\" for properties...\n");
            String line = "";
            StreamReader filereader = new StreamReader(configFile);
            //temporary Dictionary containing expandable Lists as values - will later convert back to string array
            Dictionary<String, List<String>> tmpProperties = new Dictionary<String, List<String>>();
            String key = "";
            //bool inLines = false;
            List<String> values = new List<String>();
            while ((line = filereader.ReadLine()) != null)
            {
                line.Trim();
                if (line.StartsWith("#") || line.Equals(""))
                    continue;
                else if (line.StartsWith("@@")) //this is the key
                {
                    key = line.Replace("@", "");
                    tmpProperties.Add(key, new List<String>());
                    continue;
                }
                else
                {
                    tmpProperties[key].Add(line);
                }
            }
            //convert list values backt to string arrays
            properties = new Dictionary<String, String[]>();
            MessageHandler tmpMh = new MessageHandler(false);
            foreach (KeyValuePair<String, List<String>> kvp in tmpProperties)
            {
                String logValue = "";
                String tmpKey = kvp.Key;
                List<String> tmpValue = kvp.Value;
                if (tmpValue.Count > 1)
                    logValue = "[Multiple Values]";
                else if (tmpValue.Count == 0)
                    logValue = "[Not Defined]";
                else
                    logValue = tmpValue[0];
                tmpMh.indent(tmpKey + ": " + logValue);
                this.properties.Add(tmpKey, tmpValue.ToArray());
            }
            filereader.Close();
        }

        private void processCmdArgs(String[] args)
        {
            if (args == null || args.Length < 1)
                return;
            Console.WriteLine("\nCommand Line override argument(s) detected..\n");
            String[] tmpArgs = new String[2];
            cmdArgs = new Dictionary<string, string>();
            foreach (String arg in args)
            {
                if (!Regex.IsMatch(arg, @"^.*\w=\w.*$"))
                {
                    MessageHandler tmpMh = new MessageHandler(false);
                    tmpMh.error("Invalid Argument! Arguments must be of the form 'KEY=VALUE'");
                    tmpMh.printUsage();
                    tmpMh.exitError("");
                }
                tmpArgs = arg.Split('=');
                cmdArgs.Add(tmpArgs[0].Trim(), tmpArgs[1].Trim());
                Console.WriteLine("  " + arg);
            }
        }

        private String getArgValue(String key)
        {
            key.Trim();
            if (cmdArgs == null)
                return null;
            foreach (KeyValuePair<string, string> kvp in cmdArgs)
            {
                if (kvp.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase))
                    return kvp.Value;
            }
            return null;
        }
    }
}
