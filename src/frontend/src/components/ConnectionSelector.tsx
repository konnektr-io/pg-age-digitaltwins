import { useState } from "react";
import { useConnectionStore } from "@/stores/connectionStore";
import {
  Select,
  SelectTrigger,
  SelectValue,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectLabel,
} from "@/components/ui/select";
import {
  Dialog,
  DialogTrigger,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
  DialogClose,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Button } from "@/components/ui/button";

export function ConnectionSelector(): React.ReactElement {
  const connections = useConnectionStore((state) => state.connections);
  const currentConnectionId = useConnectionStore(
    (state) => state.currentConnectionId
  );
  const setCurrentConnection = useConnectionStore(
    (state) => state.setCurrentConnection
  );
  const addConnection = useConnectionStore((state) => state.addConnection);

  const [open, setOpen] = useState(false);
  const [form, setForm] = useState({ name: "", adtHost: "", description: "" });
  const [error, setError] = useState<string | null>(null);

  const handleAdd = () => {
    if (!form.name.trim() || !form.adtHost.trim()) {
      setError("Name and Host are required.");
      return;
    }
    addConnection({
      id: form.name.toLowerCase().replace(/\s+/g, "-"),
      name: form.name,
      adtHost: form.adtHost,
      description: form.description,
    });
    setCurrentConnection(form.name.toLowerCase().replace(/\s+/g, "-"));
    setForm({ name: "", adtHost: "", description: "" });
    setError(null);
    setOpen(false);
  };

  return (
    <div className="flex items-center gap-2">
      <Select
        value={currentConnectionId || ""}
        onValueChange={setCurrentConnection}
      >
        <SelectTrigger className="min-w-[180px]">
          <SelectValue placeholder="Select connection..." />
        </SelectTrigger>
        <SelectContent>
          <SelectGroup>
            <SelectLabel>Connections</SelectLabel>
            {connections.map((conn) => (
              <SelectItem key={conn.id} value={conn.id}>
                {conn.name}
              </SelectItem>
            ))}
          </SelectGroup>
        </SelectContent>
      </Select>
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogTrigger asChild>
          <Button variant="outline" size="sm" className="ml-1">
            Add
          </Button>
        </DialogTrigger>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Add Connection</DialogTitle>
          </DialogHeader>
          <form
            onSubmit={(e) => {
              e.preventDefault();
              handleAdd();
            }}
            className="space-y-4"
          >
            <div>
              <Label htmlFor="conn-name">Name</Label>
              <Input
                id="conn-name"
                value={form.name}
                onChange={(e) =>
                  setForm((f) => ({ ...f, name: e.target.value }))
                }
                placeholder="e.g. Local Dev"
                required
              />
            </div>
            <div>
              <Label htmlFor="conn-host">Host</Label>
              <Input
                id="conn-host"
                value={form.adtHost}
                onChange={(e) =>
                  setForm((f) => ({ ...f, adtHost: e.target.value }))
                }
                placeholder="e.g. localhost:5000"
                required
              />
            </div>
            <div>
              <Label htmlFor="conn-desc">Description</Label>
              <Input
                id="conn-desc"
                value={form.description}
                onChange={(e) =>
                  setForm((f) => ({ ...f, description: e.target.value }))
                }
                placeholder="Optional"
              />
            </div>
            {error && <div className="text-red-500 text-sm">{error}</div>}
            <DialogFooter>
              <Button type="submit">Add Connection</Button>
              <DialogClose asChild>
                <Button type="button" variant="ghost">
                  Cancel
                </Button>
              </DialogClose>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
