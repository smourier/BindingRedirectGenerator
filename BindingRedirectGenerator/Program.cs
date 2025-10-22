using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using BindingRedirectGenerator.Utilities;
using Mono.Cecil;

// we use cecil because System.Reflection.MetaData crashes...

namespace BindingRedirectGenerator
{
    public static class Program
    {
        /// <summary> Xml Namespace </summary>
        public const string ns = "urn:schemas-microsoft-com:asm.v1";

        public static readonly XName NameDependentAssembly = XName.Get("dependentAssembly", ns);
        public static readonly XName NameAssemblyIdentity = XName.Get("assemblyIdentity", ns);
        public static readonly XName NameBindingRedirect = XName.Get("bindingRedirect", ns);

        /// <inheritdoc cref="ReWriteBindingRedirects"/>
        public static void Main(string[] args)
        {
            try
            {
                if (args.Length < 1) {
                    AssemblyName entryAssembly = Assembly.GetEntryAssembly()!.GetName();
                    Console.WriteLine(entryAssembly.Name + " <config file path> [<search directory path>]");
                    return;
                }

                var outputFilePath = new FileInfo(Path.GetFullPath(args[0]));
                var inputDirectoryPath = args.Length > 1 ? new DirectoryInfo(args[1]) : null;

                Console.WriteLine("Input       : " + inputDirectoryPath);
                Console.WriteLine("Output      : " + outputFilePath);
                Console.WriteLine();

                ReWriteBindingRedirects(outputFilePath);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
            }
        }

        /// <summary> Rewrites binding Redirects in the <paramref name="configFile"/> for all Assemblies (DLLs and EXEs) in the <paramref name="searchDirectory"/> </summary>
        public static void ReWriteBindingRedirects(FileInfo configFile, DirectoryInfo? searchDirectory = null)
        {
            var doc = configFile.Exists ? XDocument.Load(configFile.FullName) : new XDocument();

            searchDirectory ??= configFile.Directory;
            var configuration = doc.GetOrCreateElement("configuration");
            var runtime = configuration.GetOrCreateElement("runtime");
            var assemblyBinding = runtime.GetOrCreateElement(XName.Get("assemblyBinding", ns));

            foreach (var file in searchDirectory!.EnumerateFiles("*.*", SearchOption.AllDirectories))
            {
                var ext = file.Extension;
                if (!".dll".EqualsIgnoreCase(ext) && 
                    !".exe".EqualsIgnoreCase(ext))
                {
                    Trace.TraceInformation("Ignoring: " + file);
                    continue;
                }

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
                    cultureFunc = i => i.Attribute("culture") == null || i.Attribute("culture")?.Value == "neutral";
                }
                else
                {
                    cultureFunc = i => i.Attribute("culture")?.Value == asm.Name.Culture;
                }

                var dependentAssembly = assemblyBinding
                    .Descendants(NameDependentAssembly)
                    .Descendants(NameAssemblyIdentity)
                    .FirstOrDefault(i => i.Attribute("name")?.Value == asm.Name.Name && i.Attribute("publicKeyToken")?.Value == pkt && cultureFunc(i))?
                    .Parent;
                if (dependentAssembly != null)
                {
                    Console.WriteLine("Ignoring '" + file + "' Binding Redirect already exists");
                    continue;
                }

                dependentAssembly = new XElement(NameDependentAssembly);
                assemblyBinding.Add(dependentAssembly);

                var assemblyIdentity = new XElement(NameAssemblyIdentity);
                dependentAssembly.Add(assemblyIdentity);
                assemblyIdentity.SetAttributeValue("name", asm.Name.Name);
                assemblyIdentity.SetAttributeValue("publicKeyToken", pkt);
                if (!string.IsNullOrEmpty(asm.Name.Culture))
                {
                    assemblyIdentity.SetAttributeValue("culture", asm.Name.Culture);
                }

                var bindingRedirect = new XElement(NameBindingRedirect);
                dependentAssembly.Add(bindingRedirect);
                bindingRedirect.SetAttributeValue("oldVersion", "0.0.0.0-65535.65535.65535.65535");
                bindingRedirect.SetAttributeValue("newVersion", asm.Name.Version.ToString());
            }

            doc.Save(configFile.FullName);
        }

        /// <summary> Gets the first <see cref="XElement"/> with the <paramref name="xName"/> from the <paramref name="parent"/>
        /// or creates and adds it to the <paramref name="parent"/> </summary>
        /// <returns> the found or created Element. </returns>
        public static XElement GetOrCreateElement(this XContainer parent, XName xName)
        {
            var configuration = parent.Element(xName);
            if (configuration == null)
            {
                configuration = new XElement(xName);
                parent.Add(configuration);
            }

            return configuration;
        }

        public static AssemblyDefinition? LoadAssembly(this FileInfo path)
        {
            try
            {
                return AssemblyDefinition.ReadAssembly(path.FullName);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Error reading '" + path + "': " + e.Message);
                return null;
            }
        }

    }
}
