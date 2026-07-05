import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Separator } from "@/components/ui/separator"

const values = [
  {
    title: "Sustainability first",
    description:
      "We process offcuts, storm-damaged wood, and reclaimed timber instead of harvesting solely for chips, cutting waste at the source.",
  },
  {
    title: "Consistent quality",
    description:
      "Every batch is screened for size and moisture content so what you order is what shows up on site.",
  },
  {
    title: "Community rooted",
    description:
      "We work with local arborists, sawmills, and landscapers to keep material moving through the region instead of the landfill.",
  },
]

export function About() {
  return (
    <div className="mx-auto max-w-4xl px-4 py-16 sm:px-6">
      <h1 className="text-3xl font-bold tracking-tight sm:text-4xl">
        Who We Are
      </h1>
      <p className="mt-6 text-lg text-muted-foreground">
        Timberline Woodchips has been turning local timber and wood waste into
        useful, high-quality woodchips for homeowners, landscapers, and
        businesses. What started as a small chipping operation has grown into
        a full-service supplier &mdash; but we still process every load with
        the same care as our first delivery.
      </p>

      <Separator className="my-10" />

      <h2 className="text-2xl font-semibold tracking-tight">What We Do</h2>
      <p className="mt-4 text-muted-foreground">
        We source, chip, screen, and deliver woodchips for a wide range of
        uses &mdash; from garden mulch to biomass fuel. Our team works
        directly with local sawmills, tree services, and landowners to source
        material responsibly, then process it on-site to the size and
        specification each use case demands.
      </p>

      <Separator className="my-10" />

      <h2 className="text-2xl font-semibold tracking-tight">What We Value</h2>
      <div className="mt-6 grid gap-6 sm:grid-cols-3">
        {values.map((value) => (
          <Card key={value.title}>
            <CardHeader>
              <CardTitle className="text-lg">{value.title}</CardTitle>
            </CardHeader>
            <CardContent className="text-sm text-muted-foreground">
              {value.description}
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  )
}
