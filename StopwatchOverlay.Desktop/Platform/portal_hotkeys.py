"""Persistent portal session; communicate activations to the desktop process."""
import json
import sys
import uuid
from gi.repository import Gio, GLib

BUS_NAME = "org.freedesktop.portal.Desktop"
OBJECT = "/org/freedesktop/portal/desktop"
INTERFACE = "org.freedesktop.portal.GlobalShortcuts"
bus = Gio.bus_get_sync(Gio.BusType.SESSION, None)
loop = GLib.MainLoop()
session = None
pending = {}


def request(method, signature, args, callback):
    token = "stopwatch_" + uuid.uuid4().hex
    path = OBJECT + "/request/" + bus.get_unique_name()[1:].replace(".", "_") + "/" + token
    options = {"handle_token": GLib.Variant("s", token)}
    if method == "CreateSession":
        options["session_handle_token"] = GLib.Variant("s", "stopwatch_" + uuid.uuid4().hex)
    pending[path] = callback
    result = bus.call_sync(BUS_NAME, OBJECT, INTERFACE, method,
        GLib.Variant(signature, (*args, options)), GLib.VariantType.new("(o)"),
        Gio.DBusCallFlags.NONE, 10000, None)
    returned = result.unpack()[0]
    if returned != path:
        pending[returned] = pending.pop(path)


def response(connection, sender, path, interface, signal, parameters, data):
    callback = pending.pop(path, None)
    if callback is None:
        return
    status, results = parameters.unpack()
    if status != 0:
        print("ERROR Portal request was cancelled or denied: " + str(status), flush=True)
        loop.quit()
        return
    try:
        callback(results)
    except Exception as error:
        print("ERROR " + str(error), flush=True)
        loop.quit()


def created(results):
    global session
    session = results["session_handle"]
    shortcuts = [(item["id"], {
        "description": GLib.Variant("s", item["description"]),
        "preferred_trigger": GLib.Variant("s", item["trigger"])
    }) for item in json.loads(sys.argv[1])]
    request("BindShortcuts", "(oa(sa{sv})sa{sv})", (session, shortcuts, ""), bound)


def bound(results):
    shortcuts = results.get("shortcuts", [])
    if not shortcuts:
        print("ERROR No shortcuts were granted by the desktop.", flush=True)
        loop.quit()
    else:
        print("READY " + json.dumps(shortcuts), flush=True)


def activated(connection, sender, path, interface, signal, parameters, data):
    handle, shortcut_id, timestamp, options = parameters.unpack()
    if handle == session:
        print("HOTKEY " + shortcut_id, flush=True)


def closed(connection, sender, path, interface, signal, parameters, data):
    if path == session:
        loop.quit()


def parent_closed(source, condition):
    loop.quit()
    return False


bus.signal_subscribe(BUS_NAME, "org.freedesktop.portal.Request", "Response", None, None,
                     Gio.DBusSignalFlags.NONE, response, None)
bus.signal_subscribe(BUS_NAME, INTERFACE, "Activated", OBJECT, None,
                     Gio.DBusSignalFlags.NONE, activated, None)
bus.signal_subscribe(BUS_NAME, "org.freedesktop.portal.Session", "Closed", None, None,
                     Gio.DBusSignalFlags.NONE, closed, None)
GLib.io_add_watch(sys.stdin, GLib.IO_HUP, parent_closed)
try:
    bus.call_sync(BUS_NAME, OBJECT, "org.freedesktop.host.portal.Registry", "Register",
                  GLib.Variant("(sa{sv})", ("stopwatch-overlay", {})), None,
                  Gio.DBusCallFlags.NONE, 10000, None)
    request("CreateSession", "(a{sv})", (), created)
    loop.run()
except Exception as error:
    print("ERROR " + str(error), flush=True)
finally:
    if session:
        try:
            bus.call_sync(BUS_NAME, session, "org.freedesktop.portal.Session", "Close", None,
                          None, Gio.DBusCallFlags.NONE, 1000, None)
        except GLib.Error:
            pass
