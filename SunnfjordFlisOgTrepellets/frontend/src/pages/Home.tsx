import { Link } from "react-router-dom"
import { Leaf, Truck, Recycle } from "lucide-react"

import { Button } from "@/components/ui/button"
import {
  Card,
  CardHeader,
  CardTitle,
  CardDescription,
} from "@/components/ui/card"
import { products } from "@/data/products"

const highlights = [
  {
    icon: Leaf,
    title: "Locally Sourced",
    description:
      "Every chip starts as timber harvested and processed within the region, keeping our footprint small.",
  },
  {
    icon: Recycle,
    title: "Sustainably Produced",
    description:
      "We chip offcuts and reclaimed wood that would otherwise go to waste, turning it into something useful.",
  },
  {
    icon: Truck,
    title: "Reliable Delivery",
    description:
      "Bulk or bagged, we get product to your site on schedule, rain or shine.",
  },
]

export function Home() {
  return (
    <div>
      <section className="mx-auto max-w-6xl px-4 py-20 text-center sm:px-6 sm:py-28">
        <h1 className="text-4xl font-bold tracking-tight sm:text-6xl">
          Quality Woodchips, Grown and Ground Locally
        </h1>
        <p className="mx-auto mt-6 max-w-2xl text-lg text-muted-foreground">
          Timberline Woodchips supplies mulch, landscaping, and fuel-grade
          woodchips to homeowners, landscapers, and businesses. Sustainably
          sourced, reliably delivered.
        </p>
        <div className="mt-8 flex flex-wrap items-center justify-center gap-3">
          <Button size="lg" asChild>
            <Link to="/products">View Our Products</Link>
          </Button>
          <Button size="lg" variant="outline" asChild>
            <Link to="/about">Learn About Us</Link>
          </Button>
        </div>
      </section>

      <section className="border-t border-border bg-muted/40">
        <div className="mx-auto grid max-w-6xl gap-6 px-4 py-16 sm:grid-cols-3 sm:px-6">
          {highlights.map(({ icon: Icon, title, description }) => (
            <Card key={title}>
              <CardHeader>
                <Icon className="size-8 text-primary" />
                <CardTitle className="mt-2">{title}</CardTitle>
                <CardDescription>{description}</CardDescription>
              </CardHeader>
            </Card>
          ))}
        </div>
      </section>

      <section className="mx-auto max-w-6xl px-4 py-16 sm:px-6">
        <div className="flex items-end justify-between gap-4">
          <div>
            <h2 className="text-2xl font-semibold tracking-tight sm:text-3xl">
              Our Products
            </h2>
            <p className="mt-2 text-muted-foreground">
              A quick look at what we offer &mdash; see the full lineup for
              detailed uses.
            </p>
          </div>
          <Button variant="link" asChild className="hidden shrink-0 sm:inline-flex">
            <Link to="/products">See all products &rarr;</Link>
          </Button>
        </div>
        <div className="mt-8 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {products.slice(0, 3).map((product) => (
            <Card key={product.slug}>
              <CardHeader>
                <CardTitle>{product.name}</CardTitle>
                <CardDescription>{product.description}</CardDescription>
              </CardHeader>
            </Card>
          ))}
        </div>
        <div className="mt-8 text-center sm:hidden">
          <Button variant="link" asChild>
            <Link to="/products">See all products &rarr;</Link>
          </Button>
        </div>
      </section>
    </div>
  )
}
