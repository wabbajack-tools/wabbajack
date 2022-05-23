import os

for line in open("CHANGELOG.md").readlines():
    if (line.startswith("#### Version")):
        print(line.split(" ")[3])
        os.environ["VERSION"] = line.split(" ")[3]
        break