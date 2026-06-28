import { Label } from "@/components/ui/label"
import { MAX_NAME_TEXT_LENGTH } from "@/types"

export interface NameListInputProps {
  value: string
  onChange: (value: string) => void
}

export function namesFromText(text: string): string[] {
  return text
    .split("\n")
    .map((line) => line.trim())
    .filter((line) => line.length > 0)
}

export function NameListInput({ value, onChange }: NameListInputProps) {
  const tooLong = namesFromText(value).filter((name) => name.length > MAX_NAME_TEXT_LENGTH)

  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor="name-list">Names (one per line)</Label>
      <textarea
        id="name-list"
        className="flex min-h-32 w-full rounded-md border border-input bg-background px-3 py-2 text-sm shadow-sm placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
        placeholder={"Alice\nBob\nCharlie"}
        value={value}
        onChange={(e) => onChange(e.target.value)}
      />
      <p className="text-xs text-muted-foreground">Max {MAX_NAME_TEXT_LENGTH} characters per name.</p>
      {tooLong.length > 0 && (
        <p className="text-xs text-destructive">
          Too long: {tooLong.join(", ")}
        </p>
      )}
    </div>
  )
}
