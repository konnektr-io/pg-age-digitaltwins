import { useState } from "react";
import { Clock, Play, Trash2, Search } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Badge } from "@/components/ui/badge";
import { useQueryStore } from "@/stores/queryStore";
import { formatDistanceToNow } from "date-fns";

export function QueryHistory() {
  const { queryHistory, setCurrentQuery, clearHistory, executeQuery } =
    useQueryStore();
  const [searchQuery, setSearchQuery] = useState("");

  const filteredHistory = queryHistory.filter((item) =>
    item.query.toLowerCase().includes(searchQuery.toLowerCase())
  );

  const handleRunQuery = async (query: string) => {
    setCurrentQuery(query);
    await executeQuery(query);
  };

  const formatExecutionTime = (ms: number) => {
    if (ms < 1000) return `${ms}ms`;
    return `${(ms / 1000).toFixed(1)}s`;
  };

  return (
    <div className="flex flex-col h-full w-full bg-card border-l border-border">
      {/* History Header */}
      <div className="p-3 border-b border-border">
        <div className="flex items-center justify-between mb-3">
          <h3 className="font-semibold text-sm flex items-center gap-2">
            <Clock className="w-4 h-4" />
            Query History
          </h3>
          <Button
            variant="ghost"
            size="sm"
            onClick={clearHistory}
            disabled={queryHistory.length === 0}
            className="text-xs"
          >
            <Trash2 className="w-3 h-3" />
          </Button>
        </div>

        {/* Search */}
        <div className="relative">
          <Search className="absolute left-2 top-2 h-3 w-3 text-muted-foreground" />
          <Input
            placeholder="Search queries..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="pl-7 text-xs h-8"
          />
        </div>
      </div>

      {/* History List */}
      <ScrollArea className="flex-1">
        <div className="p-2 space-y-2">
          {filteredHistory.length === 0 ? (
            <div className="text-center text-muted-foreground text-xs py-8">
              {queryHistory.length === 0 ? (
                <>
                  <Clock className="w-6 h-6 mx-auto mb-2 opacity-50" />
                  <p>No queries in history</p>
                </>
              ) : (
                <p>No queries match your search</p>
              )}
            </div>
          ) : (
            filteredHistory.map((item, index) => (
              <div
                key={index}
                className="group border rounded-md p-3 hover:bg-muted/50 cursor-pointer transition-colors"
                onClick={() => setCurrentQuery(item.query)}
              >
                {/* Query Preview */}
                <div className="text-xs font-mono text-foreground mb-2 line-clamp-2">
                  {item.query}
                </div>

                {/* Query Metadata */}
                <div className="flex items-center justify-between text-xs text-muted-foreground">
                  <div className="flex items-center gap-2">
                    <span>
                      {formatDistanceToNow(new Date(item.timestamp), {
                        addSuffix: true,
                      })}
                    </span>
                    {item.executionTime && (
                      <Badge variant="outline" className="text-xs">
                        {formatExecutionTime(item.executionTime)}
                      </Badge>
                    )}
                    {item.resultCount !== undefined && (
                      <Badge variant="outline" className="text-xs">
                        {item.resultCount} rows
                      </Badge>
                    )}
                  </div>

                  {/* Run Button */}
                  <Button
                    variant="ghost"
                    size="sm"
                    className="opacity-0 group-hover:opacity-100 transition-opacity p-1"
                    onClick={(e) => {
                      e.stopPropagation();
                      handleRunQuery(item.query);
                    }}
                    title="Run this query"
                  >
                    <Play className="w-3 h-3" />
                  </Button>
                </div>
              </div>
            ))
          )}
        </div>
      </ScrollArea>
    </div>
  );
}
