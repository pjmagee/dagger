# This file generated by `dagger_codegen`. Please DO NOT EDIT.
defmodule Dagger.ScalarTypeDef do
  @moduledoc "A definition of a custom scalar defined in a Module."

  use Dagger.Core.QueryBuilder

  @derive Dagger.ID

  defstruct [:selection, :client]

  @type t() :: %__MODULE__{}

  @doc "A doc string for the scalar, if any."
  @spec description(t()) :: {:ok, String.t()} | {:error, term()}
  def description(%__MODULE__{} = scalar_type_def) do
    selection =
      scalar_type_def.selection |> select("description")

    execute(selection, scalar_type_def.client)
  end

  @doc "A unique identifier for this ScalarTypeDef."
  @spec id(t()) :: {:ok, Dagger.ScalarTypeDefID.t()} | {:error, term()}
  def id(%__MODULE__{} = scalar_type_def) do
    selection =
      scalar_type_def.selection |> select("id")

    execute(selection, scalar_type_def.client)
  end

  @doc "The name of the scalar."
  @spec name(t()) :: {:ok, String.t()} | {:error, term()}
  def name(%__MODULE__{} = scalar_type_def) do
    selection =
      scalar_type_def.selection |> select("name")

    execute(selection, scalar_type_def.client)
  end

  @doc "If this ScalarTypeDef is associated with a Module, the name of the module. Unset otherwise."
  @spec source_module_name(t()) :: {:ok, String.t()} | {:error, term()}
  def source_module_name(%__MODULE__{} = scalar_type_def) do
    selection =
      scalar_type_def.selection |> select("sourceModuleName")

    execute(selection, scalar_type_def.client)
  end
end
