using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.Build.BuildEngine;
using Microsoft.Win32;

namespace GenVSTGTs
{
    class Program
    {
        private static MessageHandler mh;
        private static Properties properties;
        private static String config;
        private static String omProject;
        private static String baseDir;
        private static List<String> includeDeps;
        private static List<String> excludeDeps;
        private static List<String> allDeps;
        private static int skippedTGTsCount = 0;
        private static int createdTGTsCount = 0;

        static void Main(String[] args)
        {
            properties = new Properties(args);
            mh = properties.getMessageHandler();
            config = properties.getConfig();
            omProject = properties.getOMProject();
            baseDir = properties.getBaseDir();
            String tgtMode = "Full";
            if (properties.isIncremental())
                tgtMode = "Incremental";
            Console.WriteLine("\nGenerating TGTs in " + tgtMode + " mode.");
            List<String> dependencies = findDependencies(baseDir);
            if (dependencies.Count == 0)
                mh.warn("No TGTs to generate. No matching dependencies were found. Please check your Recursive, Includes and Excludes settings in the TGT Generation Configuration file.");
            Console.WriteLine("\n########## Auto Generating Visual Studio TGTs ##########\n");
            String[] slnDependencies = dependencies.FindAll(new Predicate<String>(testForSln)).ToArray();
            String[] projDependencies = dependencies.FindAll(new Predicate<String>(testForProj)).ToArray();
            if (slnDependencies.Length > 0)
                createSlnTGTs(slnDependencies);
            if (projDependencies.Length > 0)
                createProjTGTs(projDependencies);
            if (Environment.ExitCode > 0)
                Console.WriteLine("\n########## Visual Studio TGT Generation Completed With Errrors ##########\n");
            else
            {
                Console.WriteLine("\n########## Visual Studio TGT Generation Completed ##########\n");
            }
            mh.indent("TGTs Created: " + createdTGTsCount);
            mh.indent("TGTs Skipped: " + skippedTGTsCount);
            mh.indent("Warnings: " + mh.getWarningCount());
            mh.indent("Errors: " + mh.getErrorCount() + "\n");
        }

        private static bool testForSln(String dep)
        {
            if (dep.EndsWith(".sln", StringComparison.InvariantCultureIgnoreCase))
                return true;
            else
                return false;
        }

        private static bool testForProj(String dep)
        {
            if (dep.EndsWith("proj", StringComparison.InvariantCultureIgnoreCase))
                return true;
            else
                return false;

        }

        private static String getTargetName(String depName, String depDir)
        {
            return getTargetName(depName, depDir, properties.getConfig());
        }

        private static String getTargetName(String depName, String depDir, String configuration)
        {
            String targetName = null;
            mh.debugPrint("Getting target name for dependency " + depName + " and configuration " + configuration);

            if (!File.Exists(depName))
            {
                skippedTGTsCount++;
                mh.error("Skipping target creation for project \"" + depName + "\". Could not locate derived file on file system.");
                return "";
            }

            if (depName.EndsWith(".vcproj", true, null))
            {
                targetName = getVCTargetName(depName, configuration);
            }
            else if (depName.EndsWith(".csproj", true, null) || depName.EndsWith(".vbproj", true, null))
            {
                // must get MSBuild bin path in order to parse MSBuild Project Files
                Engine.GlobalEngine.BinPath = getMSBuildBin();
                targetName = getVBCSTargetName(depName, configuration);
            }

            if (targetName == null || targetName.Equals(""))
            {
                skippedTGTsCount++;
                mh.warn("Skipping target creation for project \"" + depName + "\"");
                return "";
            }
            targetName = scrubTarget(targetName, depName, depDir);
            return targetName;
        }

        private static String getVBCSTargetName(String vbcsProj, String configuration)
        {
            String targetName = null;
            String outputPath = null;
            config = properties.getConfig();

            // Create a new empty project
            Project project = new Project();

            // Load a project
            try
            {
                project.Load(vbcsProj);
            }
            catch (Exception e)
            {
                mh.error("Problems loading project file! " + e.Message);
                return "";
            }

            vbcsProj = project.FullFileName;
            Dictionary<String, BuildPropertyGroup> config2PropGroup = new Dictionary<String, BuildPropertyGroup>();

            targetName = project.GetEvaluatedProperty("TargetFileName");
            if (targetName.Equals("") || targetName == null)
            {
                mh.error("Could not resolve TargetFileName value in Project \"" + vbcsProj + "\"");
                return "";
            }
            // Iterate through the various property groups and subsequently 
            // through the various properties
            foreach (BuildPropertyGroup propertyGroup in project.PropertyGroups)
            {
                String cond = propertyGroup.Condition;
                if (!cond.Equals("") && (cond.IndexOf("Configuration", StringComparison.InvariantCultureIgnoreCase) >= 0) && (cond.IndexOf("==", StringComparison.InvariantCultureIgnoreCase) >= 0))
                {
                    int platformIndex = cond.LastIndexOf("|");
                    int cfgIndex = cond.LastIndexOf("==") + 4;
                    int substrLength = platformIndex - cfgIndex;
                    String confSubstr = cond.Substring(cfgIndex, substrLength);
                    if (config2PropGroup.ContainsKey(confSubstr))
                        continue;
                    config2PropGroup.Add(confSubstr, propertyGroup);
                }
            }

            String cfgKey = null;
            String firstCfgKey = null;
            BuildPropertyGroup bpgMatch = null;
            BuildPropertyGroup firstBpgMatch = null;
            bool foundExactMatch = false;
            bool foundCloseMatch = false;
            int i = 0;
            foreach (KeyValuePair<string, BuildPropertyGroup> kvp in config2PropGroup)
            {
                String tmpCfg = kvp.Key;
                config = properties.getConfig();
                if (i == 0)
                {
                    firstCfgKey = tmpCfg;
                    firstBpgMatch = kvp.Value;
                    i++;
                }
                if (tmpCfg.Equals(config, StringComparison.InvariantCultureIgnoreCase))
                {
                    bpgMatch = kvp.Value;
                    cfgKey = tmpCfg;
                    foundExactMatch = true;
                    break;
                }
                else if (tmpCfg.IndexOf(config, StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    bpgMatch = kvp.Value;
                    cfgKey = tmpCfg;
                    foundCloseMatch = true;
                }
            }
            if (!foundExactMatch && foundCloseMatch) //may enhance later to try to derive alternative config matches
            {
                mh.warn("Could not find configuration match for \"" + config + "\" in \"" + vbcsProj + "\"");
                Console.WriteLine("Please create TGT either manually in the Meister GUI or by changing the default configuration passed to the TGT generator.");
                return "";
                //mh.warn("Could not find exact configuration match for: \"" + config + "\". Using configuration \"" + cfgKey + "\"");
            }
            else if (!foundExactMatch && !foundCloseMatch) //may enhance later to try to derive alternative config matches
            {
                mh.warn("Could not find configuration match for \"" + config + "\" in \"" + vbcsProj + "\"");
                Console.WriteLine("Please create TGT either manually in the Meister GUI or by changing the default configuration to the generator");
                return "";
                //cfgKey = firstCfgKey;
                //bpgMatch = firstBpgMatch;
                //mh.warn("Could not find a close configuration match for: \"" + config + "\". Using first configuration found \"" + cfgKey + "\"");
            }

            foreach (BuildProperty prop in bpgMatch)
            {
                if (prop.Name.Equals("OutputPath", StringComparison.InvariantCultureIgnoreCase))
                {
                    //Console.WriteLine("{0}:{1}", prop.Name, prop.Value);
                    outputPath = prop.Value;
                    if (!(outputPath.EndsWith("\\") || outputPath.EndsWith("/")) && !outputPath.Equals(""))
                        outputPath += Path.DirectorySeparatorChar;
                    outputPath = scrubConfigPath(outputPath, cfgKey);
                    targetName = outputPath + targetName;
                    break;
                }
            }

            mh.debugPrint("Configuration used is \"" + cfgKey + "\"");
            return targetName;
        }
        private static String getVCTargetName(String vcProj, String configuration)
        {
            String targetName = null;
            String outputDir = null;
            String outputFile = null;
            String configType = null;
            config = properties.getConfig();

            XmlTextReader reader = new XmlTextReader(vcProj);
            bool inConfig = false;
            Dictionary<String, Dictionary<String, String>> config2Props = new Dictionary<String, Dictionary<String, String>>();
            String configKey = "";
            while (reader.Read())
            {
                if (reader.NodeType.Equals(XmlNodeType.Element) && reader.Name.Equals("VisualStudioProject"))
                {
                    while (reader.MoveToNextAttribute()) // Read the attributes.
                        if (reader.Name.Equals("Name", StringComparison.InvariantCultureIgnoreCase))
                            targetName = reader.Value;
                }
                if (reader.NodeType.Equals(XmlNodeType.Element) && reader.Name.Equals("Configuration"))
                {
                    inConfig = true;
                    while (reader.MoveToNextAttribute()) // Read the attributes.
                    {
                        if (reader.Name.Equals("Name"))
                        {
                            configKey = reader.Value.Trim();
                            configKey = configKey.Substring(0, configKey.IndexOf("|"));
                            config2Props.Add(configKey, new Dictionary<String, String>());
                        }
                        else if (reader.Name.Equals("OutputDirectory"))
                            config2Props[configKey].Add("OutputDirectory", reader.Value.Trim());
                        else if (reader.Name.Equals("ConfigurationType"))
                            config2Props[configKey].Add("ConfigurationType", reader.Value.Trim());
                    }
                }
                if (inConfig && reader.NodeType.Equals(XmlNodeType.Element) && reader.Name.Equals("Tool") && reader.HasAttributes)
                    while (reader.MoveToNextAttribute())
                    {
                        if (reader.Name.Equals("OutputFile"))
                        {
                            if (reader.HasValue)
                                config2Props[configKey].Add("OutputFile", reader.Value.Trim());
                        }
                    }
            }

            if (targetName == null || targetName.Equals(""))
            {
                mh.error("Could not find a Target Name match in \"" + vcProj + "\"");
                return "";
            }

            String cfgKey = null;
            String firstCfgKey = null;
            Dictionary<String, String> propsMatch = null;
            Dictionary<String, String> firstPropsMatch = null;
            bool foundExactMatch = false;
            bool foundCloseMatch = false;
            int i = 0;
            foreach (KeyValuePair<string, Dictionary<String, String>> kvp in config2Props)
            {
                String tmpCfg = kvp.Key;
                if (i == 0)
                {
                    firstCfgKey = tmpCfg;
                    firstPropsMatch = kvp.Value;
                    i++;
                }
                if (tmpCfg.Equals(configuration, StringComparison.InvariantCultureIgnoreCase))
                {
                    propsMatch = kvp.Value;
                    cfgKey = tmpCfg;
                    foundExactMatch = true;
                    break;
                }
                else if (tmpCfg.IndexOf(configuration, StringComparison.InvariantCultureIgnoreCase) >= 0)
                {
                    propsMatch = kvp.Value;
                    cfgKey = tmpCfg;
                    foundCloseMatch = true;
                }
            }
            if (!foundExactMatch && foundCloseMatch) //may enhance later to try to derive alternative config matches
            {
                mh.warn("Could not find configuration match for \"" + config + "\" in \"" + vcProj + "\"");
                Console.WriteLine("Please create TGT either manually in the Meister GUI or by changing the default configuration to the generator");
                return "";
                //mh.warn("Could not find exact configuration match for: \"" + config + "\". Using configuration \"" + cfgKey + "\"");
            }
            else if (!foundExactMatch && !foundCloseMatch) //may enhance later to try to derive alternative config matches
            {
                mh.warn("Could not find configuration match for \"" + config + "\" in \"" + vcProj + "\"");
                Console.WriteLine("Please create TGT either manually in the Meister GUI or by changing the default configuration passed to the generator.");
                return "";
                //cfgKey = firstCfgKey;
                //propsMatch = firstPropsMatch;
                //mh.warn("Could not find a close configuration match for: \"" + config + "\". Using first configuration found \"" + cfgKey + "\"");
            }

            String targetExt = null;
            Dictionary<String, String> propsDictionary;
            propsDictionary = config2Props[cfgKey];
            if (propsDictionary.ContainsKey("ConfigurationType"))
                configType = propsDictionary["ConfigurationType"];
            if (configType == null || configType.Equals(0))
            {
                mh.error("Could not find a \"ConfigurationType\" match in \"" + vcProj + "\"");
                return "";
            }
            switch (configType)
            {
                case "1":
                    targetExt = ".exe";
                    break;
                case "2":
                    targetExt = ".dll";
                    break;
                case "4":
                    targetExt = ".lib";
                    break;
            }
            if (propsDictionary.ContainsKey("OutputDirectory"))
                outputDir = propsDictionary["OutputDirectory"];
            if (outputDir == null || outputDir.Equals(""))
            {
                mh.warn("Could not find an \"OutputDirectory\" match in \"" + vcProj + "\"");
                return "";
            }
            outputDir = scrubConfigPath(outputDir, cfgKey);
            if (propsDictionary.ContainsKey("OutputFile"))
                outputFile = propsDictionary["OutputFile"];
            if (outputFile != null && outputFile != "")
            {
                outputFile = outputFile.Replace("$(OutDir)", outputDir);
                targetName = scrubConfigPath(outputFile, cfgKey);
            }
            else
            {
                if (!(outputDir.EndsWith("\\") && outputDir.EndsWith("/")))
                    outputDir += Path.DirectorySeparatorChar;
                targetName = outputDir + targetName + targetExt;
            }
            mh.debugPrint("Configuration used is \"" + cfgKey + "\"");
            return targetName;
        }

        private static void createSlnTGTs(String[] slns)
        {
            foreach (String sln in slns)
            {
                List<String> projsMatched = new List<String>();
                StreamReader filereader = new StreamReader(sln);
                Dictionary<String, String> projID2proj = new Dictionary<String, String>();
                String line = "";
                config = properties.getConfig();
                bool inProjs = false;
                bool inGlobal = false;
                bool tgtCreated = false;
                Match match = Regex.Match(sln, @"^.*\\", RegexOptions.None);
                int slnDirLength = match.Length;
                String relSln = sln.Substring(slnDirLength);
                String slnPath = Path.GetDirectoryName(Path.GetFullPath(sln));
                while ((line = filereader.ReadLine()) != null)
                {
                    line = Regex.Replace(line, @"^\s+", "");
                    line = Regex.Replace(line, @"\s+$", "");
                    if (!inGlobal && line.StartsWith("Project(\""))
                    {
                        inProjs = true;
                        //split by "=" and grab the last half of the line which contains the project information
                        String[] tmpArray = line.Split('=');
                        //get last element
                        String parseLine = tmpArray[tmpArray.Length - 1];
                        parseLine = parseLine.Replace("\"", "");
                        //parseLine.Replace("\\", "");
                        //last half of line contains 3 items separated by commas: project name , relative path to project , project ID
                        tmpArray = parseLine.Split(',');
                        //grab the second element in the array which will be the relative path to the project
                        parseLine = tmpArray[1];
                        parseLine = Regex.Replace(parseLine, @"^\s+", "");
                        parseLine = Regex.Replace(parseLine, @"\s+$", "");
                        if (parseLine.IndexOf("Solution Items") >= 0)
                            continue;
                        /*- need to grab the proj ID so we can check later if the target is selected to be built in sln file
                        - if it is not, we will not generate a tgt. */
                        String projID = tmpArray[2];
                        projID = Regex.Replace(projID, @"^\s+", "");
                        projID = Regex.Replace(projID, @"\s+$", "");
                        if (!projID2proj.ContainsKey(projID))
                            projID2proj.Add(projID, parseLine);
                    }
                    else if (inProjs && line.StartsWith("GlobalSection(ProjectConfiguration"))
                    {
                        inGlobal = true;
                        inProjs = false;
                        //Regex objNotNaturalPattern=new Regex("[^0-9]");
                    }
                    else if (!inProjs && inGlobal && (line.IndexOf("Build.0", StringComparison.InvariantCultureIgnoreCase) > 0))
                    {
                        foreach (KeyValuePair<string, string> kvp in projID2proj)
                        {
                            String tmpKey = kvp.Key;
                            String thisRelSln = relSln;
                            bool isExclude = false;
                            if (line.IndexOf(tmpKey + "." + config + "|", StringComparison.InvariantCultureIgnoreCase) >= 0)
                            {
                                //sometimes the actual active config for the solution build is not the same as
                                //the config match. The active config should always be, however, the
                                //config listed after the last "=" sign before the last "|"
                                //e.g. {1ECFFA7A-1B8D-4D6B-8065-6A37A5359139}.Debug|Win32.Build.0 = Debug MFC Static|Win32
                                //the "Debug" sln build will actually build the "Debug MFC Static" config for this project.
                                int activeConfigStartIndex = line.LastIndexOf("=") + 1;
                                int activeConfigEndIndex = line.LastIndexOf("|");
                                String tmpConfig = line.Substring(activeConfigStartIndex, activeConfigEndIndex - activeConfigStartIndex);
                                tmpConfig = tmpConfig.Trim();
                                if (projsMatched.Contains(tmpKey))
                                    break;
                                else
                                    projsMatched.Add(tmpKey);
                                String relProjName = kvp.Value;
                                projID2proj.Remove(relProjName);
                                String project = slnPath + Path.DirectorySeparatorChar + relProjName;
                                String projPath = Path.GetDirectoryName(project);
                                String projDir = projPath.Substring(slnPath.Length);
                                //String projDir = projPath.Replace(slnPath, "");
                                projDir = Regex.Replace(projDir, @"^\\", "");
                                projDir = Regex.Replace(projDir, @"^.\\", "");
                                project = Regex.Replace(project, @"\\\\", "\\");//just in case a doubling of back slashes
                                //look for exclude matches of projects
                                String projName = Regex.Replace(project, @"^.*\\", "");
                                if (excludeDeps.Count > 0)
                                    foreach (String excludeDep in excludeDeps)
                                        if (Regex.IsMatch(excludeDep, projName))
                                        {
                                            isExclude = true;
                                            break;
                                        }
                                if (isExclude)
                                    continue;
                                String targetName = getTargetName(project, projDir, tmpConfig);
                                if (targetName.Equals(""))
                                    continue;
                                if (targetName.Contains("$(SolutionDir)"))
                                {
                                    String slnDir = slnPath;
                                    slnDir = Regex.Replace(slnDir, @"(\\)+$", "");
                                    slnDir = Regex.Replace(slnDir, @"^.*\\", "");
                                    targetName = targetName.Replace("$(SolutionDir)", slnDir);
                                }
                                if (!projDir.Equals("")) //if there's a projDir, prepend it to the targetName
                                    targetName = projDir + Path.DirectorySeparatorChar + targetName;
                                if (properties.isRel2BaseDir())
                                {
                                    String rel2BaseDirPath = getRel2BaseDirPath(sln);
                                    targetName = rel2BaseDirPath + Path.DirectorySeparatorChar + targetName;
                                    thisRelSln = rel2BaseDirPath + Path.DirectorySeparatorChar + relSln;
                                    relProjName = rel2BaseDirPath + Path.DirectorySeparatorChar + relProjName;
                                }
                                createTGT(targetName, slnPath, thisRelSln, relProjName, project);
                                tgtCreated = true;
                            }
                        }
                    }
                    else if (!inProjs && inGlobal && line.StartsWith("EndGlobalSection"))
                    {
                        break; //done scanning at this point
                    }
                }
                if (!tgtCreated)
                {
                    //anything left in projID2Proj dictionary has not been processed - usually because a configuration was not matched
                    foreach (String projNotProcessed in projID2proj.Values)
                    {
                        skippedTGTsCount++;
                        mh.warn("Skipping target creation for project \"" + projNotProcessed + "\". Issues processing solution "
                        + "dependency \"" + sln + "\". Check to make sure there is a buildable configuration in the solution that "
                        + "matches \"" + config + "\" for this target and that your include/exclude settings are correct.");
                    }
                }
            }
        }

        private static void createProjTGTs(String[] projs)
        {
            foreach (String proj in projs)
            {
                Match match = Regex.Match(proj, @"^.*\\", RegexOptions.None);
                String projDir = match.Value;
                int projDirLength = match.Length;
                String relProj = proj.Substring(projDirLength);
                String targetName = getTargetName(proj, projDir);
                if (targetName.Equals(""))
                    continue;
                if (properties.isRel2BaseDir())
                {
                    String rel2BaseDirPath = getRel2BaseDirPath(proj);
                    targetName = rel2BaseDirPath + Path.DirectorySeparatorChar + targetName;
                    relProj = rel2BaseDirPath + Path.DirectorySeparatorChar + relProj;
                }
                createTGT(targetName, projDir, "", relProj, proj);
            }
        }

        private static String getRel2BaseDirPath(String fullProj)
        {
            String rel2BaseDirPath = "";
            String baseDirPath = "";
            fullProj = fullProj.Replace("/", "\\");
            if (baseDir.Equals("."))
                baseDirPath = Directory.GetCurrentDirectory();
            else
                baseDirPath = baseDir;
            rel2BaseDirPath = Path.GetDirectoryName(fullProj);
            baseDirPath = baseDirPath.Replace("/", "\\");
            if (!baseDirPath.EndsWith("\\"))
                baseDirPath += "\\";
            rel2BaseDirPath = rel2BaseDirPath.Replace(baseDirPath, "");
            return rel2BaseDirPath;
        }

        private static List<String> findDependencies(string sDir)
        {
            Console.WriteLine("\nScanning file system for dependencies. This could take a while...\n");
            allDeps = new List<string>();
            includeDeps = new List<string>();
            excludeDeps = new List<string>();
            List<string> filteredResults = new List<string>();

            bool isIncs = false;
            bool isExcs = false;

            String[] fileTypes;
            if (properties.isSlnBuild())
            {
                fileTypes = new String[1] { ".sln" };
            }
            else
                fileTypes = properties.getFileTypes();
            String[] incs = properties.getIncludes();
            String[] excs = properties.getExcludes();

            if (incs != null)
            {
                mh.indent("Includes detected for scanning.");
                isIncs = true;
            }
            if (excs != null)
            {
                mh.indent("Excludes detected for scanning.");
                isExcs = true;
            }

            //
            // Store a stack of our directories.
            //
            Stack<string> directoryStack = new Stack<string>();
            directoryStack.Push(sDir);

            //
            // While there are directories to process
            //
            while (directoryStack.Count > 0)
            {
                string currentDir = directoryStack.Pop();
                mh.debugPrint("Scanning directory \"" + currentDir + "\" for dependencies");
                //
                // Add all files matching File Types, then go through all includes and excludes to determine
                // inclusion or exclusion. For now the processing is inefficient, but it is easiest for parsing.
                //
                foreach (string fileType in fileTypes)
                {
                    String searchFileType = "*";
                    if (fileType.StartsWith("*"))
                        searchFileType = fileType;
                    else
                        searchFileType += fileType;
                    foreach (string fileName in Directory.GetFiles(currentDir, searchFileType))
                    {
                        allDeps.Add(fileName);
                    }
                }
                if (isIncs)
                {
                    foreach (string inc in incs)
                    {
                        try
                        {
                            foreach (string incFileName in Directory.GetFiles(currentDir, inc))
                            {
                                includeDeps.Add(incFileName);
                            }
                        }
                        catch
                        {
                            mh.debugPrint("Could not resolve path to directory " + currentDir + " with file name " + inc);
                        }
                    }
                }
                if (isExcs)
                {
                    foreach (string exc in excs)
                    {
                        try
                        {
                            foreach (string excFileName in Directory.GetFiles(currentDir, exc))
                            {
                                excludeDeps.Add(excFileName);
                            }
                        }
                        catch
                        {
                            mh.debugPrint("Could not resolve path to directory " + currentDir + " with file name " + exc);
                        }
                    }
                }
                //
                // Add all directories at this directory if recursive is on.
                //
                if (properties.isRecursive())
                {
                    foreach (string directoryName in Directory.GetDirectories(currentDir))
                    {
                        directoryStack.Push(directoryName);
                    }
                }
            }
            //now go through all results and pull out the final list
            if (!isIncs && !isExcs)
                return allDeps;
            else
            {
                foreach (String allFile in allDeps)
                {
                    if (isIncs)
                    {
                        if (includeDeps.IndexOf(allFile) >= 0)
                        {
                            mh.debugPrint("Adding dependency " + allFile);
                            filteredResults.Add(allFile);
                        }
                    }
                    else if (isExcs)
                    {
                        if (excludeDeps.IndexOf(allFile) < 0)
                        {
                            mh.debugPrint("Adding dependency " + allFile);
                            filteredResults.Add(allFile);
                        }
                    }
                }
                return filteredResults;
            }

        }

        private static String scrubConfigPath(String outPath, String projConfig)
        {
            outPath = outPath.Replace(projConfig, "$(CFG)");
            outPath = Regex.Replace(outPath, properties.getConfig(), "$(CFG)", RegexOptions.IgnoreCase); //also sub global config (usually the same but not always);
            outPath = outPath.Replace("$(ConfigurationName)", "$(CFG)");
            outPath = Regex.Replace(outPath, @"^/", "");
            outPath = Regex.Replace(outPath, @"^\\", "");
            outPath = Regex.Replace(outPath, @"^./", "");
            outPath = Regex.Replace(outPath, @"^.\\", "");
            outPath = Regex.Replace(outPath, @"/", "\\");
            outPath = Regex.Replace(outPath, @"\\\\", "\\");
            return outPath;
        }

        private static String scrubTarget(String target, String projectName, String projectDir)
        {
            projectName = Regex.Replace(projectName,@"^.*\\", "");
            projectName = Regex.Replace(projectName,@"^.*/", "");
            projectName = Regex.Replace(projectName,@"\.\w+$", "");
            target = target.Replace("$(ProjectName)", projectName);
            target = target.Replace("$(ProjectDir)", projectDir);
            target = target.Replace("$(InputDir)", projectDir);
            target = Regex.Replace(target, @"^/", "");
            target = Regex.Replace(target, @"^\\", "");
            target = Regex.Replace(target, @"^./", "");
            target = Regex.Replace(target, @"^.\\", "");
            target = Regex.Replace(target, @"/", "\\");
            target = Regex.Replace(target, @"\\\\", "\\");
            return target;
        }

        private static void createTGT(String targetName, String writeDir, String relSlnName, String relProjName, String fullProjName)
        {
            mh.debugPrint("Creating TGT for target\n " + targetName + "\nwrite dir\n " + writeDir + "\nrelative solution\n " + relSlnName + "\nrelative project\n " + relProjName + "\nfull project\n " + fullProjName);
            String tgtDir = properties.getTGTDir();
            if (tgtDir.Equals(""))
                tgtDir = writeDir;

            tgtDir = Regex.Replace(tgtDir, @"(\\)+$", ""); //strip off any trailing slashes

            // in cases where all deps are defined relative to basedir and where there are strange relative paths to
            // projs and targets, (i.e. containing many ".." levels), we can attempt to get cleaner relative paths
            // by using GetFullPath and then stripping off the leading basedir.
            if (properties.isRel2BaseDir())
            {
                if (relProjName.Contains("..")) //if there are any weird ups, overs and downs in the path, try to get the actual path location
                {
                    String fullProj = baseDir + "\\" + relProjName;
                    fullProj = Path.GetFullPath(fullProj);
                    relProjName = fullProj.Replace(baseDir + "\\", ""); //after getting full path, restrip off baseDir to get relpath
                }
                if (targetName.Contains("..")) //if there are any weird ups, overs and downs in the path, try to get the actual path location
                {
                    String fullTarget = baseDir + "\\" + targetName;
                    fullTarget = Path.GetFullPath(fullTarget);
                    targetName = fullTarget.Replace(baseDir + "\\", ""); //after getting full path, restrip off baseDir to get relpath
                }
            }


            targetName = targetName.Replace("/", "\\"); //just in case any forward slashes still around

            //prep the TGT name
            String tgtName = targetName;
            tgtName = Regex.Replace(tgtName, @"^.*\\", "");
            tgtName.Replace(" ", "_"); //replace any empty spaces in name with _'s
            tgtName += ".tgt";

            String tgtFile = tgtDir + Path.DirectorySeparatorChar + tgtName;

            if (properties.isIncremental() && File.Exists(tgtFile) && (getLastWriteTime(tgtFile) > getLastWriteTime(fullProjName)))
            {
                skippedTGTsCount++;
                Console.WriteLine("\n***** TGT \"" + tgtName + "\" up to date. *****");
                mh.indent("Project dependency \"" + fullProjName + "\" has not changed since last TGT generation.");
                Console.WriteLine("***** TGT Generation Skipped *****\n");
                return;
            }
            Dictionary<String, String> buildTypeMappings = properties.getBuildServiceMappings();
            Match extMatch = Regex.Match(targetName, @"\.\w+$", RegexOptions.None);
            String ext = extMatch.Value;
            String buildType = "";
            foreach (KeyValuePair<string, string> kvp in buildTypeMappings)
            {
                if (kvp.Key.Equals(ext, StringComparison.InvariantCultureIgnoreCase))
                    buildType = kvp.Value;
            }

            String buildTask = "";
            if (properties.isSlnBuild())
                buildTask = "Solution Build";
            else
                buildTask = "MSBuild";

            Console.WriteLine("\n***** Creating TGT \"" + tgtName + "\" *****");
            mh.indent("TGT Directory: " + tgtDir);
            mh.indent("Target Name: " + targetName);
            mh.indent("Build Service: " + buildType);
            if (properties.isSlnBuild())
                mh.indent("Solution File: " + relSlnName);
            mh.indent("Project File: " + relProjName);

            String option = null; //place holder for later addition of options
            String OMTGTOptions = "";
            if (option != null)
            {
                OMTGTOptions = "   <Option>\r\n"
           + "     <Flag></Flag>\r\n" //could add options variable here
           + "   	<Parameter></Parameter>\r\n"
           + "   	<Description></Description>\r\n"
           + "		<Type>376</Type>\r\n" // 256 + 64 + 32 + 16 + 8 
           + "   </Option>\r\n";
            }

            String OMTGTHead = "<?xml version=\"1.0\" ?>\r\n<OMTarget>\r\n <Version>6.3</Version>\r\n"
           + " <Name>" + targetName.Trim() + "</Name>\r\n"
           + " <Project>" + omProject + "</Project>\r\n"
           + " <TargetDefinitionFile>" + tgtName.Trim() + "</TargetDefinitionFile>\r\n"
           + " <OSPlatform>Windows</OSPlatform>\r\n <BuildType>" + buildType + "</BuildType>\r\n"
           + " <IntDirectory />\r\n"
           + " <PhoneyTarget>false</PhoneyTarget>\r\n"
           + " <BuildTask>\r\n"
           + "  <Name>" + buildTask + "</Name>\r\n"
           + "  <OptionGroup>\r\n"
           + "   <GroupName>Build Task Options</GroupName>\r\n"
           + "   <Type>0</Type>\r\n"
           + OMTGTOptions
           + "  </OptionGroup>\r\n"
           + " </BuildTask>\r\n";


            String OMTGTDep = " <Dependency>\r\n"
            + "  <Name>" + relProjName.Trim() + "</Name>\r\n"
            + "  <Type>1</Type>\r\n"
            + "  <ParentBuildTask>" + buildTask + "</ParentBuildTask>\r\n"
            + "  <ParentOptionGroup>Build Task Options</ParentOptionGroup>\r\n"
            + " </Dependency>\r\n";

            if (properties.isSlnBuild())
            {
                OMTGTDep += " <Dependency>\r\n"
            + "  <Name>" + relSlnName.Trim() + "</Name>\r\n"
            + "  <Type>1</Type>\r\n"
            + "  <ParentBuildTask>" + buildTask + "</ParentBuildTask>\r\n"
            + "  <ParentOptionGroup>Build Task Options</ParentOptionGroup>\r\n"
            + " </Dependency>\r\n";
            }

            String OMTGTTail = "</OMTarget>\r\n";
            String tgt = OMTGTHead + OMTGTDep + OMTGTTail;
            StreamWriter fileWriter = new StreamWriter(tgtFile);

            try
            {
                fileWriter.Write(tgt);
                if (File.Exists(tgtFile))
                {
                    Console.WriteLine("***** TGT Created  Successfully *****\n");
                    createdTGTsCount++;
                }
            }
            catch (Exception e)
            {
                mh.error("Problems writing TGT file!\n" + e.Message);
                Console.WriteLine("***** TGT Created  With Errors *****\n");
            }

            fileWriter.Close();
        }

        private static int getLastWriteTime(String file)
        {
            DateTime newDateTime = File.GetLastWriteTime(file);
            TimeSpan t = (newDateTime.ToUniversalTime() - new DateTime(1970, 1, 1));
            int timestamp = (int)t.TotalSeconds;
            return timestamp;
        }


        private static String getMSBuildBin()
        {
            // Determine msbuildBin. Try a number of ways. look for .NET framework
            // install root directory like C:\WINDOWS\Microsoft.NET\Framework\
            // eventually need to turn into C:\WINDOWS\Microsoft.NET\Framework\v2.0.50727\
            // files
            //  Microsoft.CSharp.targets
            //  Microsoft.VisualBasic.targets
            // need to be in this directory
            String install_root = null;
            String msbuild_bin = null;
            String msbuildBin = null;

            // need to look for VS 9 first.
            bool existsVS9 = false;
            try
            {
                RegistryKey key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\VisualStudio\\9.0\\MSBuild");
                if (key != null)
                {
                    existsVS9 = true;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Caught exception\n" + e.Message);
            }

            try
            {
                RegistryKey key = null;
                if (!existsVS9)
                    key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\VisualStudio\\8.0\\MSBuild");
                if (key != null)
                {
                    msbuild_bin = key.GetValue("MSBuildBinPath").ToString();
                    if (msbuild_bin == null || msbuild_bin.Length == 0) // JAG - 10.25.07 - case IUD-54
                    {
                        throw new System.ArgumentNullException();
                    }
                }
                else
                {
                    throw new System.ArgumentNullException(); // JAG - 10.25.07 - case IUD-54
                }
            }
            catch
            {
                try
                {
                    RegistryKey key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\.NETFramework");
                    if (key != null)
                    {
                        install_root = key.GetValue("InstallRoot").ToString();
                        if (install_root == null || install_root.Length == 0)
                            throw new System.ArgumentNullException();  // JAG - 10.25.07 - case IUD-54
                    }
                    else
                        throw new System.ArgumentNullException(); // JAG - 10.25.07 - case IUD-54

                }
                catch
                {
                    // try via env Variable
                    if (install_root == null)
                    {
                        try
                        {
                            String windir = System.Environment.GetEnvironmentVariable("windir");
                            install_root = Path.Combine(windir, "Microsoft.NET\\Framework");
                            if (!Directory.Exists(install_root))
                            {
                                install_root = null;
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Caught exception " + e.Message);
                        }
                    }
                }
            }

            // if we have the msbuildbinpath via VS2005, great
            if (msbuild_bin != null)
            {
                msbuildBin = msbuild_bin;
            }
            else if (install_root != null)
            {
                install_root = install_root.TrimEnd('\\');
                // look in the directory for the 3.5 and 3.0 .NET framework versions
                String[] dirs = Directory.GetDirectories(install_root, "v?.*", SearchOption.TopDirectoryOnly);
                System.Array.Reverse(dirs); // reverse the array to get the latest
                foreach (String dir in dirs)
                {
                    if (File.Exists(Path.Combine(dir, "Microsoft.CSharp.targets")) &&
                         File.Exists(Path.Combine(dir, "Microsoft.VisualBasic.targets")))
                    {
                        msbuildBin = dir;
                        break;
                    }
                }
            }

            // error here
            if (msbuildBin == null || !Directory.Exists(msbuildBin))
            {
                mh.exitError("Cannot locate install of MSBuild Engine.");
                //return 1;
            }
            return msbuildBin;
        }
    }
}

