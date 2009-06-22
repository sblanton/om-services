using System;
using System.Collections.Generic;
using System.Text;

namespace GenVSTGTs
{
    public class MessageHandler
    {
        private int errorCount;
        private int warningCount;
        private bool debug = false;
        public MessageHandler(bool debug)
        {
            this.debug = debug;
            this.errorCount = 0;
            this.warningCount = 0;
        }
        public void error(String message)
        {
            this.errorCount++;
            Console.WriteLine("\nERROR: " + message + "\n");
            Environment.ExitCode += 1;
        }

        public void warn(String message)
        {
            this.warningCount++;
            Console.WriteLine("\nWARNING: " + message + "\n");
        }

        public void debugPrint(String message)
        {
            if (debug)
            {
                Console.WriteLine("\nDEBUG: " + message + "\n");
            }
        }

        public void indent(String message)
        {
            Console.WriteLine("  " + message);
        }

        public void exitError(String message)
        {
            this.errorCount++;
            int code = 1;
            if (message != "")
                Console.WriteLine("\nGenVSTGTs exited with errors: " + message + "\n");
            Environment.ExitCode = code;
            Environment.Exit(code);
        }

        public void exitSuccess(String message)
        {
            int code = 0;
            if (message != "")
                Console.WriteLine("\nGenVSTGTs completed successfully: " + message + "\n");
            Environment.ExitCode = code;
            Environment.Exit(code);
        }

        public void printUsage()
        {
            Console.WriteLine("\nUSAGE: GenVSTGTs is a Visual Studio 2005/2008 OpenMake TGT Generator. It utilizes the 'GenVSTGTs.config' file "
                + "(found in the utility's 'bin' directory) to identify common attributes required to generate TGTs, such as "
                + "Build Service mapping information, OM Project defaults, scanning modes, and include or exclude dependency patterns. "
                + "You may open the 'GenVSTGTs.config' file in a text editor for viewing and editing default settings. "
                + "All arguments in the 'GenVSTGTs.config' are based on key words beginning with the @@ signs. The values are in the "
                + "lines that follow each of the key words.");
            Console.WriteLine("\nIt is possible to override any of the settings in the configuration file which have only one value "
                + "(most are singular) by passing in any of the key names, followed by an equals (=) sign, followed by the desired value.");
            Console.WriteLine("\nEXAMPLE: To run the TGT Generator in the non-default 'Debug' configuration with recursive scanning turned off for the Project "
                + "'SAMPLE VSBUILD', one would run the following GenVSTGTs command:");
            Console.WriteLine("\n     GenVSTGTs CFG=DEBUG Recursive=N OMProject=\"SAMPLE VSBUILD\"\n\n");

        }

        public int getErrorCount()
        {
            return this.errorCount;
        }

        public int getWarningCount()
        {
            return this.warningCount;
        }

    }

}
