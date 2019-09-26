# Contributing to Wabbajack

The following is a set of guidelines for contributing to the `halgari/wabbajack` repo on GitHub. These are guidelines but not rules so be free to propose changes.

## How Can I Contribute?

You don't have to be a programmer to contribute to this project.

### Reporting Bugs

When you encounter problems with the application, go to our [discord](https://discord.gg/zgbrkmA) server first and ask for help there. Before creating a new Issue, take a look at the others to avoid getting the [Duplicate](https://github.com/halgari/wabbajack/labels/duplicate) label.

Creating a bug report is as easy as navigating to the [Issues](https://github.com/halgari/wabbajack/issues) page and clicking the [New Issue](https://github.com/halgari/wabbajack/issues/new/choose) button.

#### Submitting A Good Bug Report

* Select the Bug report template to get started.
* **Use a clear and descriptive title** for the issue to identify the problem.
* **Describe the exact steps which reproduce the problem** in as many details as possible. Trace the steps you took and **don't just say what you did, but explain how you did it**.
* **Include additional data** in the issue. This encompasses your operating system, the version of Wabbajack and your log file.
* **Upload the stacktrace or your entire log file** to the issue using the [Code Highlighting](https://github.com/adam-p/markdown-here/wiki/Markdown-Cheatsheet#code) feature of Markdown.

### Suggesting Enhancements

Enhancements can be everything from fixing typos to a complete revamp of documents in the repo. You can just use Github for making changes by clicking the pencil icon in the top right corner of a file.

### Code Contribution

This is where the fun begins. Wabbajack is programmed in C# so having a decent amount of knowledge in that language or in C/C++ is good to have. You also want to make sure that you have a basic understanding of the git workflow.

#### Visual Studio 2019

You can download it [here](https://visualstudio.microsoft.com/vs/) but make sure to select the Community Edition as the other ones come at a cost. When installing VS you will be prompted to select a Workload and components. You will need:

* **.NET desktop development** from the Workload tab
* **.NET Framework 4.7.2 SDK and targeting pack** from the .NET section
* **NuGet package manager** from the Code tools section
* **C# and Visual Basic** from the Development activities

The installer may have selected other options as well but these are the most important ones.

### Starting development

1) **Fork and clone the project:** go to the Github repo page, click the fork button, copy the url from the forked repo, navigate to your project folder, open Git Bash or normal command prompt and type `git clone url name` and replace url with the copied url and name with the folder name
2) **Open Wabbajack.sln** in Visual Studio 2019
3) **Download NuGet Packages** by selecting the solution and *Right Click*->*Restore NuGet Packages*

It may take a while for Visual Studio to download all packages and update all References so be patience. Once all packages are downloaded go and try building Wabbajack. If the build is successful than good job, if not head over to the *#wabbajack-development* channel on the discord and talk about your build error.

#### Coding Style

As a  C# project, you should follow the [C# Coding Style](https://github.com/dotnet/corefx/blob/master/Documentation/coding-guidelines/coding-style.md). Further more you should never submit commits to your *master* branch, even if it's just a fork. Create a new branch with a meaningful name or the name of your issue/request and commit to that.

You commits should also be elegant. Check [this](https://github.com/git-for-windows/git/wiki/Good-commits) post for good practices.

Updating your fork is important and easy. Open your terminal of choice inside the project folder and add the original repo as a new remote:

`git remote add upstream https://github.com/halgari/wabbajack.git`

Make sure that you're on your master branch:

`git checkout master`

Fetch all the branches of that remote into remote-tracking branches, such as upstream/master:

`git fetch upstream`

Rewrite your master branch so that any commits of yours that
aren't already in upstream/master are replayed on top of that
other branch:

`git rebase upstream/master`

#### Submitting Code Changes

Before you go and open a pull request, make sure that your code actually runs. Build the project with your changes and test the application with its new features against your testing modlist. This testing modlist should be an MO2 installation with some mods installed that worked on the version without your changes and was not modified since then.

If everything works as intended and you found no bug in testing, go ahead and open a pull request. Your request should contain information about why you want to change something, what you changed and how you did it.
