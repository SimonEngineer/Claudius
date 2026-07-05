export function Footer() {
  return (
    <footer className="border-t border-border">
      <div className="mx-auto flex max-w-6xl flex-col items-center justify-between gap-2 px-4 py-8 text-sm text-muted-foreground sm:flex-row sm:px-6">
        <p>&copy; {new Date().getFullYear()} Timberline Woodchips. All rights reserved.</p>
        <p>Quality woodchips, sourced and delivered locally.</p>
      </div>
    </footer>
  )
}
