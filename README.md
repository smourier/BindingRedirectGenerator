# Binding Redirect Generator
A tool to generate binding redirects from assemblies in a given path.
- Sets all Versions to 2^32 -1 = 65535 
- leaves existing Redirects alone. 
- add Redirects for all other DLLs and EXEs 

## Command Line Parameters: 
1st Parameter: Path to the app.config/web.config 
2nd Parameter: optional, Path to the bin Directory (fallback to the app.config Directory and subdirectories) 

