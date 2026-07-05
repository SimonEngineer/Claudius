import { CheckCircle2 } from "lucide-react"

import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  CardDescription,
} from "@/components/ui/card"
import { products } from "@/data/products"

export function Products() {
  return (
    <div className="mx-auto max-w-6xl px-4 py-16 sm:px-6">
      <h1 className="text-3xl font-bold tracking-tight sm:text-4xl">
        Our Products
      </h1>
      <p className="mt-4 max-w-2xl text-lg text-muted-foreground">
        Every product is screened and sized for its intended use. Not sure
        what you need? Reach out and we can help you pick the right chip for
        your project.
      </p>

      <div className="mt-10 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
        {products.map((product) => (
          <Card key={product.slug}>
            <CardHeader>
              <CardTitle>{product.name}</CardTitle>
              <CardDescription>{product.description}</CardDescription>
            </CardHeader>
            <CardContent>
              <p className="mb-2 text-sm font-medium text-foreground">
                Best used for:
              </p>
              <ul className="space-y-1.5">
                {product.bestFor.map((use) => (
                  <li
                    key={use}
                    className="flex items-start gap-2 text-sm text-muted-foreground"
                  >
                    <CheckCircle2 className="mt-0.5 size-4 shrink-0 text-primary" />
                    <span>{use}</span>
                  </li>
                ))}
              </ul>
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  )
}
