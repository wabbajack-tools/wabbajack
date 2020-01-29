"""
Simple process that uses Fria to inspect the HTTP headers, and sent data during a Skyrim/Fallout login to Bethesda.NET. That data is output to the screen and can later be used to 
allow Wabbajack to log into Bethesda.NET.
"""

import frida
import sys
from subprocess import Popen, PIPE
import psutil, time, json

known_headers = {}
shutdown = False

def on_message(message, data):
    msg_type, msg_data = message["payload"]
    if msg_type == "header":
        header, value = msg_data.split(": ");
        if header not in known_headers:
            known_headers[header] = value;
    if msg_type == "data":
        try:
            data = json.loads(msg_data)
            if "scheme" in data and "language" in data and "payload" in data:
                shutdown_and_print(data)
        except:
            return

def main(target_process):
    session = frida.attach(target_process)

    script = session.create_script("""

    // Find base address of current imported jvm.dll by main process fledge.exe
    var reqHeaders = Module.getExportByName('winhttp.dll', 'WinHttpAddRequestHeaders');

    Interceptor.attach(reqHeaders, { // Intercept calls to our SetAesDecrypt function

        // When function is called, print out its parameters
        onEnter: function (args) {
            send(['header', args[1].readUtf16String(args[2].toInt32())]);

        },
        // When function is finished
        onLeave: function (retval) {
        }
    });
    
    var reqHeaders = Module.getExportByName('winhttp.dll', 'WinHttpWriteData');
    console.log("WinHttpAddRequestHeaders: " + reqHeaders);

    Interceptor.attach(reqHeaders, { // Intercept calls to our SetAesDecrypt function

        // When function is called, print out its parameters
        onEnter: function (args) {
            send(['data', args[1].readUtf8String(args[2].toInt32())]);

        },
        // When function is finished
        onLeave: function (retval) {
        }
    });
    
    
""")
    script.on('message', on_message)
    script.load()
    
    while not shutdown:
        time.sleep(0.5);
        
    session.detach()
    
def wait_for_game(name):
    while True:
        time.sleep(1);
        for proc in psutil.process_iter():
            if proc.name() == name:
                return proc.pid;
                
def shutdown_and_print(data):
    global shutdown
    output = {"body": json.dumps(data), "headers": known_headers}
    
    print(json.dumps(output))
    
    for proc in psutil.process_iter():
        if proc.pid == pid:
            proc.kill();
            break
            
    shutdown = True;
    
    

if __name__ == '__main__':
    start = """C:\Steam\steamapps\common\Skyrim Special Edition\SkyrimSE.exe"""
    wait_for = "SkyrimSE.exe"
    if len(sys.argv) == 3:
        start = sys.argv[1];
        wait_for = sys.argv[2]
    target_process = Popen([start])
    pid = wait_for_game(wait_for);
    main(pid)