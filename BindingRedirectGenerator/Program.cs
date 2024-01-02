using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using BindingRedirectGenerator.Utilities;
using Mono.Cecil; // we use cecil because System.Reflection.MetaData crashes...

namespace BindingRedirectGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            if (Debugger.IsAttached)
            {
                SafeMain(args);
                return;
            }

            try
            {
                SafeMain(args);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        static void SafeMain(string[] args)
        {
            Console.WriteLine("BindingRedirectGenerator - Copyright (C) 2019-" + DateTime.Now.Year + " Simon Mourier. All rights reserved.");
            Console.WriteLine();
            if (CommandLine.HelpRequested || args.Length < 2)
            {
                Help();
                return;
            }

            string inputDirectoryPath = CommandLine.GetArgument<string>(0);
            string outputFilePath = CommandLine.GetArgument<string>(1);
            if (inputDirectoryPath == null || outputFilePath == null)
            {
                Help();
                return;
            }

            inputDirectoryPath = Path.GetFullPath(inputDirectoryPath);
            if (!Directory.Exists(inputDirectoryPath))
            {
                Console.WriteLine(inputDirectoryPath + " directory does not exists.");
                return;
            }

            outputFilePath = Path.GetFullPath(outputFilePath);

            Console.WriteLine("Input       : " + inputDirectoryPath);
            Console.WriteLine("Output      : " + outputFilePath);
            Console.WriteLine();

            XDocument doc;
            if (File.Exists(outputFilePath))
            {
                doc = XDocument.Load(outputFilePath);
            }
            else
            {
                doc = new XDocument();
            }

            var configuration = doc.Element("configuration");
            if (configuration == null)
            {
                configuration = new XElement("configuration");
                doc.Add(configuration);
            }

            var runtime = configuration.Element("runtime");
            if (runtime == null)
            {
                runtime = new XElement("runtime");
                configuration.Add(runtime);
            }

            var ns = "urn:schemas-microsoft-com:asm.v1";

            var assemblyBinding = runtime.Element(XName.Get("assemblyBinding", ns));
            if (assemblyBinding == null)
            {
                assemblyBinding = new XElement(XName.Get("assemblyBinding", ns));
                runtime.Add(assemblyBinding);
            }

            foreach (var file in Directory.EnumerateFiles(inputDirectoryPath))
            {
                var ext = Path.GetExtension(file);
                if (string.Compare(ext, ".dll", true) != 0 && string.Compare(ext, ".exe", true) != 0)
                    continue;

                var asm = LoadAssembly(file);
                if (asm == null) // not .NET, not valid, etc.
                    continue;

                if (asm.Name.PublicKeyToken == null || asm.Name.PublicKeyToken.Length == 0) // no strong name
                {
                    Console.WriteLine("Skipping '" + file + "': No public key token.");
                    continue;
                }

                var pkt = string.Join(string.Empty, asm.Name.PublicKeyToken.Select(i => i.ToString("x2")));
                Func<XElement, bool> cultureFunc;
                if (string.IsNullOrEmpty(asm.Name.Culture))
                {
                    cultureFunc = (i) => i.Attribute("culture") == null || i.Attribute("culture")?.Value == "neutral";
                }
                else
                {
                    cultureFunc = (i) => i.Attribute("culture")?.Value == asm.Name.Culture;
                }

                var dependentAssembly = assemblyBinding
                    .Descendants(XName.Get("dependentAssembly", ns))?
                    .Descendants(XName.Get("assemblyIdentity", ns))?
                    .FirstOrDefault(i => i.Attribute("name")?.Value == asm.Name.Name && i.Attribute("publicKeyToken")?.Value == pkt && cultureFunc(i))?
                    .Parent;
                if (dependentAssembly != null)
                    continue;

                dependentAssembly = new XElement(XName.Get("dependentAssembly", ns));
                assemblyBinding.Add(dependentAssembly);

                var assemblyIdentity = new XElement(XName.Get("assemblyIdentity", ns));
                dependentAssembly.Add(assemblyIdentity);
                assemblyIdentity.SetAttributeValue("name", asm.Name.Name);
                assemblyIdentity.SetAttributeValue("publicKeyToken", pkt);
                if (!string.IsNullOrEmpty(asm.Name.Culture))
                {
                    assemblyIdentity.SetAttributeValue("culture", asm.Name.Culture);
                }

                var bindingRedirect = new XElement(XName.Get("bindingRedirect", ns));
                dependentAssembly.Add(bindingRedirect);
                bindingRedirect.SetAttributeValue("oldVersion", "0.0.0.0-65535.65535.65535.65535");
                bindingRedirect.SetAttributeValue("newVersion", asm.Name.Version.ToString());
            }

            doc.Save(outputFilePath);
        }

        public static AssemblyDefinition LoadAssembly(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            try
            {
                using var stream = File.OpenRead(path);
                return AssemblyDefinition.ReadAssembly(path);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception e)
            {
                Console.WriteLine("Skipping '" + path + "': " + e.Message);
                return null;
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }

        static void Help()
        {
            Console.WriteLine(Assembly.GetEntryAssembly().GetName().Name.ToUpperInvariant() + " <input directory path> <output file path>");
            Console.WriteLine();
            Console.WriteLine("Description:");
            Console.WriteLine("    This tool scans a directory and merges binding redirects for all assemblies found to a .config (or xml) file.");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine();
            Console.WriteLine("    " + Assembly.GetEntryAssembly().GetName().Name.ToUpperInvariant() + " c:\\mypath\\myproject myproject.exe.config ");
            Console.WriteLine();
            Console.WriteLine("    Scans the c:\\mypath\\myproject directory for assemblies and merges binding redirects to to the myproject.exe.config file.");
            Console.WriteLine();
        }
    }
}
